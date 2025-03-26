if [[ -z $1 ]]; then
    echo Usage: ./run.sh program_file
    exit
fi

rm -rf mutants
mkdir mutants
mkdir mutants/alive
mkdir mutants/killed


echo Scanning $1 for mutation targets
dotnet ./dafny/Binaries/Dafny.dll verify $1 \
    --solver-path ./dafny/Binaries/z3 \
    --plugin ./mutdafny/bin/Debug/net8.0/mutdafny.dll > /dev/null


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
            --solver-path ./dafny/Binaries/z3 \
            --plugin ./mutdafny/bin/Debug/net8.0/mutdafny.dll,"$pos $op")
    else
        echo Mutating position $pos: operator $op, argument $arg
        output=$(dotnet ./dafny/Binaries/Dafny.dll verify $1 \
            --solver-path ./dafny/Binaries/z3 \
            --plugin ./mutdafny/bin/Debug/net8.0/mutdafny.dll,"$pos $op $arg")
    fi


    verification_finished=$(echo $output | grep "Dafny program verifier finished")
    verified=$(echo $output | grep "Dafny program verifier finished.*0 errors")
    output=$(echo $output | tail -1)

    COLOR='\033[0;31m'; if [[ -n $verified ]]; then COLOR='\033[0m'; fi
    echo -e "${COLOR}${output}\033[0m"
    if [[ -z $verification_finished ]]; # verification did not finish due to invalid program
    then 
        echo Error: mutant is invalid
    else
        output_dir=""
        if [[ -n $verified ]]; 
            then 
                echo Verification succeeded: mutant is alive
                output_dir=mutants/alive
            else 
                echo Verification failed: mutant was killed
                output_dir=mutants/killed
        fi
        mv *.dfy $output_dir
    fi
    echo
done

rm targets.csv