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
| AOR (Arithmetic Operator Replacement) | Replacement of an arithmetic operator with another | Replacement operator code |
| ROR (Relational Operator Replacement) | Replacement of a relational operator with another | Replacement operator code |
| COR (Conditional Operator Replacement) | Replacement of a conditional operator with another | Replacement operator code |
| LOR (Logical Operator Replacement) | Replacement of a logical operator with another | Replacement operator code |
| SOR (Shift Operator Replacement) | Replacement of a shift operator with another | Replacement operator code |
| BBR (Boolean-Binary Expression Replacement) | Replacement of a relational or conditional expression with `true` or `false` | Replacement value (`true` or `false`) |
| AOI (Arithmetic Operator Insertion) | Insertion of a unary minus in front of an arithmetic expression | NA |
| COI (Conditional Operator Insertion) | Insertion of a not operator in front of a conditional expression | NA |
| LOI (Logical Operator Insertion) | Insertion of a not operator in front of a logical expression | NA |
| AOD (Arithmetic Operator Deletion) | Deletion of a unary minus in front of an arithmetic expression | NA |
| COD (Conditional Operator Deletion) | Deletion of a not operator in front of a conditional expression | NA |
| LOD (Logical Operator Deletion) | Deletion of a not operator in front of a logical expression | NA |
| LVR (Literal Value Replacement) | Replacement of a numerical literal value with its increment, decrement and zero and of a string literal value either an empty one, a default one, or a mutation of the original | Replacement value |
| EVR (Expression Value Replacement) | Replacement of an expression with a default literal value of its type  | Type |
| VER (Variable Expression Replacement) | Replacement of a variable with another of the same type | The name of the replacement variable |
| LSR (Loop Statement Replacement) | Replacement of `continue` with `break` and of `break` with ether `continue` or `return` | Type of replacement statement (`continue`, `break` or `return`) |
| LBI (Loop Break Insertion) | Insertion of a `break` statement at the beggining of the body of a loop | NA |
| MRR (Method Return Value Replacement) | Replacement of a method call with a default literal of its return type | Type/list of types (for methods with multiple output variables) |
| MAP (Method Argument Propagation) | Replacement of a method call with one of its arguments with the same type as the return value | Index/list of indexes (for methods with multiple output variables) referring to the position of the argument to be propagated |
| MNR (Method Naked Receiver) | Deletion of a class method call, its receiver being mantained | NA |
| MCR (Method Call Replacement) | Replacement of a method call with another method with the same signature | The name of the replacement method |
| MVR (Method-Variable Replacement) | Replacement of a method call with a variable of the same type | The name/list of names (for methods with multiple outputs) of the replacement variable(s) |
| SAR (Swap Argument) | Swap a method call argument with another used in the same method call with the same type | The position of the replacement argument |
| CIR (Collection Initialization Replacement) | Replacement of non-empty collection initializers with an empty one and of empty initializers with a default non-empty one | NA (for empty initialization) or type of the collection's elements |
| CBR (Case Block Replacement) | Replacement of match statement cases with the default one and of the default label with one provided by the programmer | NA |
| CBE (Case Block Extraction) | Extraction of one of the blocks of an if or if-then-else statement to the outside scope and deletion of the remaining ones  | NA |
| TAR (Tuple Access Repalcement) | Replacement of the index used in a tuple element access | The replacement index |
| DCR (Datatype Constructor Replacement) | Replacement of a datatype constructor with another of the same datatype and with the same signature | The name of the replacement constructor |
| FAR (Field Access Replacement) | Replacement of a class's field access with a different field of the same class | The name of the replacement field |
| SDL (Statement Deletion) | Deletion of a statement or of an entire code block | NA |
| VDL (Variable Deletion) | Deletion of all occurences of a variable | Variable name |
| SLD (Subsequence Limit Deletion) | Deletion of either the bottom or top limit of a subsequence selection expression | NA |
| ODL (Operator Deletion) | Deletion of all occurences of a binary operator (and of one of its arguments in order to preserve program validity) | Operator code and the argument to delete |
| THI (This Keyword Insertion) | Insertion of the `this` keyword in front of the use of a parameter that has the same name as a class field | NA |
| THD (This Keyword Deletion) | Deletion of the `this` keyword in front of the use of a class field that has the same name as a parameter | NA |
| AMR (Accessor Method Replacement) | Replacement of the body of an accessor (get) method with another with the same signature | The position of the method with the replacement body |
| MMR (Modifier Method Replacement) | Replacement of the body of a modifier (set) method with another with the same signature | The position of the method with the replacement body |
| PRV (Polymorphic Reference Replacement) | Replacement of a child reference assignment to a parent with a child reference of a different type | The name of the replacement variable |
| SWS (Swap Statement) | Swap a statement with the one either immediately below or above it | NA |
| SWV (Swap Variable Declaration) | Swap the RHS of a variable declaration statement with the one from the variable declaration immediately below or above it | The position of the variable declaration with the replacement RHS |
