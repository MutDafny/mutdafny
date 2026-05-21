#!/usr/bin/env bash
#
# ------------------------------------------------------------------------------
# This script generates mutants of a given Dafny program. By default, one mutation
# is applied per program, following the principles of mutation testing. We also
# provide an option to apply more than one mutation to the same program, which 
# can be useful for a variety of use cases.
#
# Usage:
# run.sh
#   <full path to the program under test, e.g., $SCRIPT_DIR/../DafnyBench/DafnyBench/dataset/ground_truth/630-dafny_tmp_tmpz2kokaiq_Solution.dfy> 
#   [--num_mutations <the number of mutations to apply to the input program, e.g., 1 (by default)>]
#   [--run_dir <the directory where the script should be run, e.g., ./ (by deafult)>]
#   [help]
# ------------------------------------------------------------------------------ General utils

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" > /dev/null 2>&1 && pwd)"

die() {
  echo "$@" >&2
  exit 1
}

# ------------------------------------------------------------------------------ Args

USAGE="Usage: ${BASH_SOURCE[0]}
   <full path to the program under test, e.g., $SCRIPT_DIR/../DafnyBench/DafnyBench/dataset/ground_truth/630-dafny_tmp_tmpz2kokaiq_Solution.dfy>
   [--num_mutations <the number of mutations to apply to the input program, e.g., 1 (by default)>]
   [--run_dir <the directory where the script should be run, e.g., ./ (by deafult)>]
   [help]"

if [ "$#" -ne "1" ] && [ "$#" -ne "3" ] && [ "$#" -ne "5" ]; then
  die "$USAGE"
fi

if [ "$#" -eq "1" ] && [ "$1" = "--help" ]; then
    echo "$USAGE"
    exit 0
fi

PROGRAM=$1;
PROGRAM="$(cd "$(dirname "$PROGRAM")" && pwd)/$(basename "$PROGRAM")"
shift
NUM_MUTS=1
RUN_DIR="./"
while [[ "$1" = --* ]]; do
  OPTION=$1; shift
  case $OPTION in
    (--num_mutations)
      NUM_MUTS=$1;
      shift;;
    (--run_dir)
      RUN_DIR=$1;
      shift;;
    (--help)
      echo "$USAGE";
      exit 0;;
    (*)
      die "$USAGE";;
  esac
done

[ -d "$RUN_DIR" ] || die "[ERROR] $RUN_DIR does not exist!"

# ------------------------------------------------------------------------------ Cleanup

mkdir -p "$SCRIPT_DIR/original"
mkdir -p "$SCRIPT_DIR/mutants"
mkdir -p "$SCRIPT_DIR/mutants/alive"
mkdir -p "$SCRIPT_DIR/mutants/timed-out"
mkdir -p "$SCRIPT_DIR/mutants/killed"
mkdir -p "$SCRIPT_DIR/mutants/invalid"

# ------------------------------------------------------------------------------ MutDafny utils

scan_program() {
    echo Scanning $PROGRAM for mutation targets
    dotnet "$SCRIPT_DIR/dafny/Binaries/Dafny.dll" verify $PROGRAM \
        --solver-path "$SCRIPT_DIR/dafny/Binaries/z3" --allow-warnings \
        --plugin "$SCRIPT_DIR/mutdafny/bin/Debug/net8.0/mutdafny.dll",scan > /dev/null
}

single_mutation() {
    local pos="$1"
    local op="$2"
    local arg="$3"

    output=""
    if [[ -z $arg ]]; 
    then 
        echo Mutating position $pos: operator $op
        output=$(dotnet "$SCRIPT_DIR/dafny/Binaries/Dafny.dll" verify $PROGRAM \
            --solver-path "$SCRIPT_DIR/dafny/Binaries/z3" --allow-warnings \
            --plugin "$SCRIPT_DIR/mutdafny/bin/Debug/net8.0/mutdafny.dll","mut $pos $op" 2>/dev/null)
    else
        echo Mutating position $pos: operator $op, argument $arg
        output=$(dotnet "$SCRIPT_DIR/dafny/Binaries/Dafny.dll" verify $PROGRAM \
            --solver-path "$SCRIPT_DIR/dafny/Binaries/z3" --allow-warnings \
            --plugin "$SCRIPT_DIR/mutdafny/bin/Debug/net8.0/mutdafny.dll","mut $pos $op $arg" 2>/dev/null)
    fi
    echo $output
}

multiple_mutation() {
    echo Applying $NUM_MUTS mutations to the program
    output=$(dotnet "$SCRIPT_DIR/dafny/Binaries/Dafny.dll" verify $PROGRAM \
        --solver-path "$SCRIPT_DIR/dafny/Binaries/z3" --allow-warnings \
        --plugin "$SCRIPT_DIR/mutdafny/bin/Debug/net8.0/mutdafny.dll","mut $NUM_MUTS" 2>/dev/null)
    echo $output
}

process_output() {
    local output="$1"

    verification_finished=$(echo $output | grep "Dafny program verifier finished")
    verified=$(echo $output | grep "Dafny program verifier finished.*0 errors")
    timed_out=$(echo $output | grep "Dafny program verifier finished.*time out")
    output=$(echo $output | tail -1)

    COLOR='\033[0;31m'; if [[ -n $verified ]]; then COLOR='\033[0m'; fi
    if [[ -z $verification_finished ]]; then # verification did not finish due to invalid program

        if [ -f *.dfy ]; then
            echo Error: mutant is invalid
            mv *.dfy "$SCRIPT_DIR/mutants/invalid"
        else
            echo Could not apply $NUM_MUTS mutations to the program
        fi

    elif [ -f *.dfy ]; then
        echo -e "${COLOR}${output}\033[0m"
        output_dir=""
        if [[ -n $timed_out ]]; then
            echo Verification timed out
            output_dir="$SCRIPT_DIR/mutants/timed-out"
        elif [[ -n $verified ]]; then 
            echo Verification succeeded: mutant is alive
            output_dir="$SCRIPT_DIR/mutants/alive"
        else 
            echo Verification failed: mutant was killed
            output_dir="$SCRIPT_DIR/mutants/killed"
        fi

        mv *.dfy $output_dir
    else
        echo Could not apply $NUM_MUTS mutations to the program
    fi
}

# ------------------------------------------------------------------------------ Main

pushd . > /dev/null 2>&1
cd "$RUN_DIR"
rm -rf ./targets.csv
rm -f ./*.dfy
scan_program

IFS=','
if [ "$NUM_MUTS" -eq "1" ]; then
    while read pos op arg;
    do

        output=$(single_mutation $pos $op $arg)
        mutant_type_msg=$(echo $output | head -n 1)
        echo $mutant_type_msg
        mutant_outcome_msg=$(process_output "$output")
        echo $mutant_outcome_msg
        echo
        rm elapsed-time.csv

    done < targets.csv
else
    num_targets=$(wc -l < targets.csv)
    MAX_TRIES=$(($num_targets / $NUM_MUTS * 5)) # 5 tries per mutant
    NUM_TRIES=0
    while [ $(wc -l < targets.csv 2>/dev/null || echo 0) -ge $NUM_MUTS ] && [ $NUM_TRIES -lt $MAX_TRIES ];
    do

        output=$(multiple_mutation)
        mutant_type_msg=$(echo $output | head -n 1)
        echo $mutant_type_msg
        mutant_outcome_msg=$(process_output "$output")
        echo $mutant_outcome_msg
        echo

        could_not_apply_all_muts=$(echo $mutant_outcome_msg | grep "Could not apply $NUM_MUTS mutations to the program")
        if [[ -n $could_not_apply_all_muts ]]; then
            NUM_TRIES=$((NUM_TRIES+1))
        else
            NUM_TRIES=0
            num_targets=$(wc -l < targets.csv)
            MAX_TRIES=$(($num_targets / $NUM_MUTS * 5))
        fi
        rm elapsed-time.csv

    done

    line_count=$(wc -l < targets.csv 2>/dev/null || echo 0)
    if [ "$line_count" -lt "$NUM_MUTS" ]; then 
        echo "Consumed all targets"
    else
        echo "Reached max combination tries"
    fi
fi

rm targets.csv
cp ./original/*.dfy "$SCRIPT_DIR/original"
popd > /dev/null 2>&1

echo "[INFO] Job finished"
echo "DONE!"
exit 0