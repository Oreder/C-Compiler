C Compiler in C#
=

This intends to be a full ANSI C compiler. It generates x86 (32-bit) assembly code in linux. The goal is to produce `.s` files that `gcc`'s assembler and linker could directly use.

### 1. Handwritten Scanner (lexical analysis) - done
* Not automatically generated by flex.
* Written via standard state-machine approach.

### 2. Handwritten Parser (grammar analysis) - done
* Not automatically generated by yacc / bison.
* Standard recursive descent parser, with a little hack.

### 3. Semantic Analysis - done
* A type system to perform the C language implicit typecasts and other tasks.
* An environment system to record the user defined symbols.

### 4. Code Generator - round 20%
* Generates x86 assembly code.