# MutDafny: A Mutation Testing Tool for Dafny Programs

## Running the Project

1. **Build dafny**
```
cd dafny && make exe
```

2. **Install Z3**
```
cd dafny/Binaries
wget https://github.com/dafny-lang/solver-builds/releases/download/snapshot-2023-08-02/z3-4.12.1-x64-ubuntu-20.04-bin.zip
unzip z3-4.12.1-x64-ubuntu-20.04-bin.zip
mv z3-4.12.1 z3
chmod 755 z3
```


3. **Build mutdafny**
```
cd mutdafny && dotnet build
```

4. **Run mutdafny**
```
./run.sh program_file
```