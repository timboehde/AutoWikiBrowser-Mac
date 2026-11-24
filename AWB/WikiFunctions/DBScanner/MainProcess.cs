/*
Database Scanner
Copyright (C) 2007 Martin Richards

This program is free software; you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation; either version 2 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using WikiFunctions.Background;

namespace WikiFunctions.DBScanner
{
    public delegate void StopDel();

    public class ArticleInfo
    {
        public string Title, Text, Timestamp, Restrictions;

        public bool IsFullyRead
        {
            get
            {
                return !string.IsNullOrEmpty(Title)
                && !string.IsNullOrEmpty(Timestamp)
                && Text != null;
            }
        }
    }

    class MainProcess
    {
        public event StopDel StoppedEvent;
        public CrossThreadQueue<string> OutputQueue;

        private readonly string FileName;
        private readonly string From = "";
        private Stream stream;

        private readonly SendOrPostCallback SOPCstopped;
        private SynchronizationContext Context;

        private Thread ScanThread;
        readonly List<Thread> SecondaryThreads = new List<Thread>();
        private readonly bool MultiThreaded;
        private readonly int ProcessorCount = Environment.ProcessorCount;   // caching
        private readonly CrossThreadQueue<ArticleInfo> PendingArticles = new CrossThreadQueue<ArticleInfo>();

        private readonly List<Scan> Scanners;
        private readonly bool IgnoreComments;

        public MainProcess(List<Scan> z, string[] filenames, ThreadPriority tp, bool ignoreComments, string startFrom)
            : this(z, filenames, tp, ignoreComments)
        {
            From = startFrom;
        }

        public MainProcess(List<Scan> z, string[] filenames, ThreadPriority tp, bool ignoreComments)
        {
            ProcessorCount = Environment.ProcessorCount; // caching
            SOPCstopped = Stopped;
            Priority = tp;
            IgnoreComments = ignoreComments;
            MultiThreaded = ProcessorCount > 1;

            Scanners = z;

            try
            {
                if (filenames.Length == 1)
                {
                    FileName = filenames[0];
                    stream = new FileStream(FileName, FileMode.Open, FileAccess.Read);
                }
                else
                {
                    FileName = Path.GetDirectoryName(filenames[0]);
                    stream = new MultiXmlStream(filenames);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// Gets the percentage of scan complete, ranging from 0 to 1
        /// </summary>
        public double PercentageComplete
        {
            get
            {
                try
                {
                    lock (ScanThread)
                    {
                        if (stream == null)
                            return 1; // scan complete

                        if (stream.Length == 0)
                            return 0;

                        return (double)stream.Position / stream.Length;
                    }
                }
                // ObjectDisposedException is still possible if we exited the main loop in Process() due to exception
                catch (ObjectDisposedException) { } // ignore

                return 1; // scan ended, probably in fire
            }
        }

        public string CurrentFile
        {
            get
            {
                if (stream is MultiXmlStream multiStream)
                    return multiStream.CurrentName;
                else
                    return FileName;
            }
        }

        private void Stopped(object o)
        {
            StoppedEvent();
        }

        public void Stop()
        {
            Run = false;
            if (ScanThread == null) return;

            ScanThread.Abort();
            foreach (Thread thr in SecondaryThreads)
            {
                thr.Abort();
            }
            ScanThread.Join();

            foreach (Thread thr in SecondaryThreads)
            {
                // avoid deadlocks when calling from secondary thread
                if (thr.ManagedThreadId != Thread.CurrentThread.ManagedThreadId)
                    thr.Join();
            }
        }

        public void Start()
        {
            Context = SynchronizationContext.Current;

            ScanThread = new Thread(Process)
                             {
                                 Name = "DB Scanner thread", 
                                 IsBackground = true, 
                                 Priority = mPriority
                             };
            ScanThread.Start();

            for (int i = 0; i < ProcessorCount - 1; i++)
            {
                Thread thr = new Thread(SecondaryThread)
                                 {
                                     Name = "DB Scanner thread #" + (i + 2),
                                     IsBackground = true,
                                     Priority = mPriority
                                 };
                SecondaryThreads.Add(thr);
                thr.Start();
            }
        }

        private void ScanArticle(ArticleInfo ai)
        {
            if (IgnoreComments)
                ai.Text = WikiRegexes.Comments.Replace(ai.Text, "");

            foreach (Scan z in Scanners)
            {
                if (!z.Check(ai))
                {
                    return;
                }
            }

            OutputQueue.Add(ai.Title);
        }

        private void Process()
        {
            string articleTitle = "";

            try
            {
                using (XmlTextReader reader = new XmlTextReader(stream))
                {
                    reader.WhitespaceHandling = WhitespaceHandling.None;

                    if (From.Length > 0)
                    {
                        // move to start from article
                        while (Run && reader.Read())
                        {
                            if (reader.NodeType != XmlNodeType.Element)
                                continue;

                            if (reader.Name != "title")
                            {
                                reader.ReadToFollowing("page");
                                continue;
                            }

                            // reader.ReadToFollowing("title");
                            articleTitle = reader.ReadString();

                            if (From.Equals(articleTitle))
                                break;
                        }
                    }

                    while (Run)
                    {
                        ArticleInfo ai = ReadArticle(reader);
                        if (ai == null) break;
                        articleTitle = ai.Title;

                        // we must maintain a huge enough buffer to safeguard against fluctuations
                        // of page size
                        if (MultiThreaded && (PendingArticles.Count < ProcessorCount * 10))
                            PendingArticles.Add(ai);
                        else
                            ScanArticle(ai);
                    }

                    lock (ScanThread)
                    {
                        stream = null;
                    }

                    if (MultiThreaded)
                    {
                        while (PendingArticles.Count > 0)
                            Thread.Sleep(10);

                        Run = false;

                        foreach (Thread thr in SecondaryThreads)
                            thr.Join();
                    }
                }
            }
            catch (ThreadAbortException) { }
            catch (XmlException ex)
            {
                if (Message)
                    System.Windows.Forms.MessageBox.Show($"XML parsing error in {CurrentFile}.\r\n\r\n{ex.Message}");
            }
            catch (Exception ex)
            {
                if (Message)
                    ErrorHandler.HandleException(ex);
            }
            finally
            {
                if (Message)
                    Context.Post(SOPCstopped, articleTitle);
            }
        }

        /// <summary>
        /// Reads a page from the reader, returns ArticleInfo or null if EOF
        /// </summary>
        private ArticleInfo ReadArticle(XmlReader reader)
        {
            do 
                if (!reader.ReadToFollowing("page")) return null;
            while (!reader.IsStartElement());

            ArticleInfo ai = new ArticleInfo();
            while (reader.Read() && reader.Name != "page") // stop on closing element
            {
                if (!reader.IsStartElement()) continue;
                switch (reader.Name)
                {
                    case "title":
                        ai.Title = reader.ReadString();
                        break;
                    case "timestamp":
                        ai.Timestamp = reader.ReadString();
                        break;
                    case "restrictions":
                        ai.Restrictions = reader.ReadString();
                        break;
                    case "text":
                        ai.Text = reader.ReadString();
                        break;
                }
            }

            return ai.IsFullyRead ? ai : null;
        }

        private void SecondaryThread()
        {
            try
            {
                while (Run)
                {
                    ArticleInfo ai = null;
                    if (PendingArticles.Count > 0) lock (PendingArticles)
                    {
                        if (PendingArticles.Count > 0)
                        {
                            ai = PendingArticles.Remove();
                        }
                    }

                    if (ai != null)
                        ScanArticle(ai);
                    else
                        Thread.Sleep(1);
                }
            }
            catch (ThreadAbortException)
            { }
            catch (Exception ex)
            {
                ErrorHandler.HandleException(ex);
            }
        }

        #region Properties

        public bool Run = true;
        public bool Message = true;

        ThreadPriority mPriority = ThreadPriority.BelowNormal;
        public ThreadPriority Priority
        {
            get { return mPriority; }
            set
            {
                mPriority = value;
                if (ScanThread != null)
                    ScanThread.Priority = value;

                foreach (Thread thr in SecondaryThreads)
                {
                    thr.Priority = value;
                }
            }
        }
        #endregion
    }

    class MultiXmlStream : Stream
    {
        // I admit Copilot helped create this class
        private string[] Sources;
        private int CurrentStreamIndex = 0;
        private Stream CurrentStream;

        // Some future implementation might need access
        public string CurrentName { get; private set; } = RootStart;

        // The dump files are UTF-8 without BOM, so we'll keep the same style.
        const string RootStart = "<root>";
        const string RootEnd = "﻿</root>";

        public MultiXmlStream(string[] files)
        {
            if (files.Length < 2)
                throw new ArgumentException("MultiXmlStream requires at least two files.");
            Sources = new string[files.Length + 2];
            Sources[0] = RootStart;
            files.CopyTo(Sources, 1);
            Sources[Sources.Length - 1] = RootEnd;
            CurrentStream = StringStream(RootStart);
            Length = files.Select(name => new FileInfo(name).Length).Sum() +
                Encoding.UTF8.GetBytes(RootStart).Length + Encoding.UTF8.GetBytes(RootEnd).Length;
            //... which are obviously the same as the string lengths, but let's do it right
            Position = 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length { get; }
        public override long Position { get; set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (CurrentStream == null) return 0;

            int bytesRead = CurrentStream.Read(buffer, offset, count);
            Position += bytesRead;
            if (bytesRead == 0)
            {
                // Move to the next file
                if (++CurrentStreamIndex < Sources.Length)
                {
                    CurrentStream.Dispose();
                    CurrentName = Sources[CurrentStreamIndex];
                    if (CurrentName.Contains(Path.DirectorySeparatorChar))
                    {
                        CurrentStream = new FileStream(CurrentName, FileMode.Open, FileAccess.Read);
                    }
                    else
                    {
                        CurrentStream = StringStream(CurrentName);
                    }
                    return Read(buffer, offset, count); // Recursive call to read from the next file
                }
                else
                {
                    CurrentStream.Dispose();
                    CurrentStream = null; // No more files to read
                    return 0;
                }
            }
            return bytesRead;
        }

        private Stream StringStream(string text)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(text));
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
