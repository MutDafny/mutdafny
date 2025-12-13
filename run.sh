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
#   [help]
# ------------------------------------------------------------------------------ General utils

die() {
  echo "$@" >&2
  exit 1
}

# ------------------------------------------------------------------------------ Args

USAGE="Usage: ${BASH_SOURCE[0]}
   <full path to the program under test, e.g., $SCRIPT_DIR/../DafnyBench/DafnyBench/dataset/ground_truth/630-dafny_tmp_tmpz2kokaiq_Solution.dfy>
   [--num_mutations <the number of mutations to apply to the input program, e.g., 1 (by default)>]
   [help]"
if [ "$#" -ne "1" ] && [ "$#" -ne "3" ]; then
  die "$USAGE"
fi

if [ "$#" -eq "1" ] && [ "$1" = "--help" ]; then
    echo "$USAGE"
    exit 0
fi

PROGRAM=$1
NUM_MUTS=1
if [ "$#" -eq "3" ]; then
    NUM_MUTS=$3
fi

# ------------------------------------------------------------------------------ Cleanup

rm -rf targets.csv
rm -rf mutants
mkdir mutants
mkdir mutants/alive
mkdir mutants/timed-out
mkdir mutants/killed

# ------------------------------------------------------------------------------ MutDafny utils

scan_program() {
    echo Scanning $PROGRAM for mutation targets
    dotnet ./dafny/Binaries/Dafny.dll verify $PROGRAM \
        --allow-warnings --solver-path ./dafny/Binaries/z3 \
        --plugin ./mutdafny/bin/Debug/net8.0/mutdafny.dll,scan > /dev/null
}

single_mutation() {
    local pos="$1"
    local op="$2"
    local arg="$3"

    if [[ -z $arg ]]; 
    then 
        echo Mutating position $pos: operator $op
        dotnet ./dafny/Binaries/Dafny.dll verify $PROGRAM \
            --allow-warnings --solver-path ./dafny/Binaries/z3 \
            --plugin ./mutdafny/bin/Debug/net8.0/mutdafny.dll,"mut $pos $op"
    else
        echo Mutating position $pos: operator $op, argument $arg
        dotnet ./dafny/Binaries/Dafny.dll verify $PROGRAM \
            --allow-warnings --solver-path ./dafny/Binaries/z3 \
            --plugin ./mutdafny/bin/Debug/net8.0/mutdafny.dll,"mut $pos $op $arg"
    fi
}

multiple_mutation() {
    echo Applying $NUM_MUTS mutations to the program
    dotnet ./dafny/Binaries/Dafny.dll verify $PROGRAM \
        --allow-warnings --solver-path ./dafny/Binaries/z3 \
        --plugin ./mutdafny/bin/Debug/net8.0/mutdafny.dll,"mut $NUM_MUTATIONS"
}

process_output() {
    local output="$1"

    verification_finished=$(echo $output | grep "Dafny program verifier finished")
    verified=$(echo $output | grep "Dafny program verifier finished.*0 errors")
    timed_out=$(echo $output | grep "Dafny program verifier finished.*time out")
    output=$(echo $output | tail -1)

    COLOR='\033[0;31m'; if [[ -n $verified ]]; then COLOR='\033[0m'; fi
    if [[ -z $verification_finished ]]; then # verification did not finish due to invalid program
        rm *.dfy
        echo Error: mutant is invalid
    else
        echo -e "${COLOR}${output}\033[0m"
        output_dir=""
        if [[ -n $timed_out ]]; then
            echo Verification timed out
            output_dir=mutants/timed-out
        elif [[ -n $verified ]]; then 
            echo Verification succeeded: mutant is alive
            output_dir=mutants/alive
        else 
            echo Verification failed: mutant was killed
            output_dir=mutants/killed
        fi
        mv *.dfy $output_dir
    fi
}

# ------------------------------------------------------------------------------ Main


scan_program

if [ "$#" -eq "1" ] || [ $NUM_MUTS -eq "1" ]; then
    IFS=','
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
    while [ ! -s targets.txt ];
    do

        output=$(multiple_mutation)
        mutant_type_msg=$(echo $output | head -n 1)
        echo $mutant_type_msg
        mutant_outcome_msg=$(process_output "$output")
        echo $mutant_outcome_msg
        echo
        rm elapsed-time.csv

    done
fi

rm targets.csv