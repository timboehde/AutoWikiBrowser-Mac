#!/bin/zsh
# Builds the Mac port and saves output to MacPort/build.log so Claude can read it.
#   ./build.sh        build the engine and the Mac app
#   ./build.sh run    build, then launch AutoWikiBrowser (app output goes to MacPort/run.log)
#   ./build.sh test   build and run AWB's unit tests (output in MacPort/test.log, results in test-results.trx)
cd "$(dirname "$0")"
case "$1" in
  test)
    dotnet test UnitTests/UnitTests.csproj -nologo -clp:ErrorsOnly \
      --logger "trx;LogFileName=$PWD/test-results.trx" --logger "console;verbosity=normal" 2>&1 | tee test.log
    echo "\nDone — results saved to MacPort/test.log and MacPort/test-results.trx"
    ;;
  *)
    dotnet build AWB.Mac/AWB.Mac.csproj -clp:ErrorsOnly -nologo 2>&1 | tee build.log
    errors=$(grep -c ' error ' build.log)
    echo "\nErrors: $errors — log saved to MacPort/build.log"
    if [[ "$1" == "run" && "$errors" == "0" ]]; then
      echo "Starting AutoWikiBrowser… (output in MacPort/run.log)"
      dotnet run --project AWB.Mac/AWB.Mac.csproj --no-build > run.log 2>&1
    fi
    ;;
esac
