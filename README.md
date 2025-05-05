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

## Mutation Operators

| **Operator**    | **Description** | **Argument** |
| -------- | ------- | ------- |
| BOR (Binary Operator Replacement) | Replacement of an arithmetic, relational, conditional, logical or shift operator with another of the same category | Replacement operator code |
| BBR (Boolean-Binary Expression Repalcement) | Replacement of a relational or conditional expression with `true` or `false` | Replacement value (`true` or `false`) |
| UOI (Unary Operator Insertion) | Insertion of a unary minus or not operator in front of an expression | Insertion operator code |
| UOD (Unary Operator Deletion) | Deletion of a unary minus or not operator in front of an expression | NA |
| LVR (Literal Value Replacement) | Replacement of a numerical literal value with its increment, decrement and zero and of a string literal value either an empty one, a default one, or a mutation of the original | Replacement value |
| EVR (Expression Value Replacement) | Replacement of an expression with a default literal value of its type  | Type |
| LSR (Loop Statement Replacement) | Replacement of `continue` with `break` and of `break` with ether `continue` or `return` | Type of replacement statement (`continue`, `break` or `return`) |
| CIR (Collection Initialization Replacement) | Replacement of non-empty collection initializers with an empty one and of empty initializers with a default non-empty one | NA (for empty initialization) or type of the collection's elements
