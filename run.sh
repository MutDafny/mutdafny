if [[ -z $1 ]]; then
    echo Usage: ./run.sh program_file
    exit
fi

rm -rf mutants
mkdir mutants
mkdir mutants/alive
mkdir mutants/timed-out
mkdir mutants/killed


echo Scanning $1 for mutation targets
dotnet ./dafny/Binaries/Dafny.dll verify $1 \
    --allow-warnings --solver-path ./dafny/Binaries/z3 \
    --plugin ./mutdafny/bin/Debug/net8.0/mutdafny.dll,scan > /dev/null


IFS=','
readarray -t targets < targets.csv
for target in "${targets[@]}"
do
    read -ra target_args <<< "$target"
    pos=${target_args[0]}
    op=${target_args[1]}
    arg=${target_args[2]}

    output=""
    if [[ -z $arg ]]; 
    then 
        echo Mutating position $pos: operator $op
        output=$(dotnet ./dafny/Binaries/Dafny.dll verify $1 \
            --allow-warnings --solver-path ./dafny/Binaries/z3 \
            --plugin ./mutdafny/bin/Debug/net8.0/mutdafny.dll,"mut $pos $op")
    else
        echo Mutating position $pos: operator $op, argument $arg
        output=$(dotnet ./dafny/Binaries/Dafny.dll verify $1 \
            --allow-warnings --solver-path ./dafny/Binaries/z3 \
            --plugin ./mutdafny/bin/Debug/net8.0/mutdafny.dll,"mut $pos $op $arg")
    fi


    verification_finished=$(echo $output | grep "Dafny program verifier finished")
    verified=$(echo $output | grep "Dafny program verifier finished.*0 errors")
    timed_out=$(echo $output | grep "Dafny program verifier finished.*time out")
    output=$(echo $output | tail -1)

    COLOR='\033[0;31m'; if [[ -n $verified ]]; then COLOR='\033[0m'; fi
    echo -e "${COLOR}${output}\033[0m"
    if [[ -z $verification_finished ]]; then # verification did not finish due to invalid program
        echo Error: mutant is invalid
    else
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
    echo
    rm elapsed-time.txt
done

rm targets.csv
rm helpers.txt