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
| VER (Variable Expression Replacement) | Replacement of a variable with another of the same type | The name of the replacement variable |
| LSR (Loop Statement Replacement) | Replacement of `continue` with `break` and of `break` with ether `continue` or `return` | Type of replacement statement (`continue`, `break` or `return`) |
| LBI (Loop Break Insertion) | Insertion of a `break` statement at the beggining of the body of a loop | NA |
| MCR (Method Call Replacement) | Replacement of a method call with a default literal, with another method with the same signature, with one of its arguments with the same type as the return value, or with its receiver | NA (for naked receiver mutation), type/list of types (for methods with multiple output variables), the name of the replacement method, or index/list of indexes referring to the position of the argument to be propagated |
| SAR (Swap Argument) | Swap a method call argument with another used in the same method call with the same type | The position of the replacement argument |
| CIR (Collection Initialization Replacement) | Replacement of non-empty collection initializers with an empty one and of empty initializers with a default non-empty one | NA (for empty initialization) or type of the collection's elements |
| CBR (Case Block Replacement) | Replacement of match statement cases with the default one and of the default label with one provided by the programmer | NA |
| CBE (Case Block Extraction) | Extraction of one of the blocks of an if or if-then-else statement to the outside scope and deletion of the remaining ones  | NA |
| DCR (Datatype Constructor Replacement) | Replacement of a datatype constructor with another of the same datatype and with the same signature | The name of the replacement constructor |
| SDL (Statement Deletion) | Deletion of a statement or of an entire code block | NA |
| VDL (Variable Deletion) | Deletion of all occurences of a variable | Variable name |
| ODL (Operator Deletion) | Deletion of all occurences of a binary operator (and of one of its arguments in order to preserve program validity) | Operator code and the argument to delete |
| THI (This Keyword Insertion) | Insertion of the `this` keyword in front of the use of a parameter that has the same name as a class field | NA |
| THD (This Keyword Deletion) | Deletion of the `this` keyword in front of the use of a class field that has the same name as a parameter | NA |
| AMR (Accessor Method Replacement) | Replacement of the body of an accessor (get) method with another with the same signature | The position of the method with the replacement body |
| MMR (Modifier Method Replacement) | Replacement of the body of a modifier (set) method with another with the same signature | The position of the method with the replacement body |
| FAR (Field Access Replacement) | Replacement of a class's field access with a different field of the same class | The name of the replacement field |
| PRV (Polymorphic Reference Replacement) | Replacement of a child reference assignment to a parent with a child reference of a different type | The name of the replacement variable |
| SWS (Swap Statement) | Swap a statement with the one either immediately below or above it | NA |
