# F# Best Practices and Common Pitfalls with .NET 8

## Table of Contents
1. [Introduction](#introduction)
2. [F# Best Practices](#f-best-practices)
   - [Code Organization](#code-organization)
   - [Type Design](#type-design)
   - [Function Design](#function-design)
   - [Performance Considerations](#performance-considerations)
3. [Common Pitfalls and How to Avoid Them](#common-pitfalls-and-how-to-avoid-them)
   - [Compiler Order Dependencies](#compiler-order-dependencies)
   - [Type Inference Issues](#type-inference-issues)
   - [Function Parameter Handling](#function-parameter-handling)
   - [Mutability and Reference Issues](#mutability-and-reference-issues)
   - [Syntax Errors](#syntax-errors)
4. [Common Compilation Errors](#common-compilation-errors)
   - [FS0001: Type Mismatch](#fs0001-type-mismatch)
   - [FS0003: This value is not a function](#fs0003-this-value-is-not-a-function)
   - [FS0025: Incomplete pattern matches](#fs0025-incomplete-pattern-matches)
   - [FS0041: A unique overload could not be determined](#fs0041-a-unique-overload-could-not-be-determined)
   - [FS0072: Lookup on object of indeterminate type](#fs0072-lookup-on-object-of-indeterminate-type)
   - [FS0588: Block following this 'let' is unfinished](#fs0588-block-following-this-let-is-unfinished)
   - [FS1161: TABs are not allowed](#fs1161-tabs-are-not-allowed)
5. [.NET 8 Specific Features and Best Practices](#net-8-specific-features-and-best-practices)
   - [F# 8 Features](#f-8-features)
   - [Performance Improvements](#performance-improvements)
   - [Interoperability with .NET 8](#interoperability-with-net-8)
6. [Advanced F# Patterns and Techniques](#advanced-f-patterns-and-techniques)
   - [Domain-Driven Design with F#](#domain-driven-design-with-f)
   - [Railway Oriented Programming](#railway-oriented-programming)
   - [Computation Expressions](#computation-expressions)
   - [Type Providers](#type-providers)
   - [Property-Based Testing](#property-based-testing)
   - [Testing Frameworks](#testing-frameworks)
7. [Tooling Tips](#tooling-tips)

## Introduction

F# is a functional-first programming language that also supports object-oriented and imperative programming models. With the release of .NET 8, F# has evolved with new features that improve code quality, performance, and developer experience.

This document covers best practices, common pitfalls, and solutions to compilation errors that will help you write better, more efficient F# code with .NET 8.

## F# Best Practices

### Code Organization

1. **Define things before they are used**
   - F# uses a single-pass compiler that processes code in order from top to bottom
   - Make sure files are compiled in the right order (listed in the .fsproj file)
   - Define functions, types, and values before they are referenced

2. **Use namespaces and modules appropriately**
   - For publicly consumable code, prefer namespaces at the top level
   - Namespaces are compiled as .NET namespaces; modules are compiled as static classes
   - Consider using `[<RequireQualifiedAccess>]` for modules to prevent name collisions
   - Example:
     ```fsharp
     // Recommended for libraries
     namespace MyCode
     
     type MyClass() = 
         // Implementation here
     ```

3. **Minimize module initialization complexity**
   - Module initialization compiles into a static constructor
   - Errors in module initialization can cause hard-to-diagnose `TypeInitializationException`
   - Consider using classes to hold dependencies:
     ```fsharp
     // Instead of complex module initialization
     type MyParametricApi(dep1, dep2, dep3) = 
         member _.Function1 arg1 = doStuffWith dep1 dep2 dep3 arg1
         member _.Function2 arg2 = doStuffWith dep1 dep2 dep3 arg2
     ```

4. **Keep helper functions private**
   - Use `[<AutoOpen>]` on a private module of helper functions if they become numerous
   - Avoid exposing internal implementation details

### Type Design

1. **Be cautious with Type Abbreviations**
   - Type abbreviations (like `type BufferSize = int`) are just aliases with no extra type safety
   - Consider using single-case discriminated unions for better type safety:
     ```fsharp
     // Instead of
     type BufferSize = int
     
     // Consider
     type BufferSize = BufferSize of int
     ```

2. **Avoid revealing concrete representations**
   - Hide implementation details behind interfaces or abstract representations
   - This decreases coupling in your code

3. **Prefer composition over inheritance**
   - Inheritance hierarchies can be complex and difficult to change
   - Use interface implementation or composition for polymorphism

4. **Use explicit type annotations for public APIs**
   - Do not rely on type inference for public interfaces
   - This gives you control over the API shape rather than letting the compiler decide
   - Makes your API more stable against internal changes

### Function Design

1. **Design for partial application**
   - Arrange function parameters to facilitate partial application
   - Place the data being operated on last

2. **Use pipeline-friendly design**
   - Design functions to work well with the `|>` operator
   - Last parameter should be the main data being transformed

3. **Favor immutability**
   - Use immutable data structures by default
   - Only use mutation when necessary for performance

4. **Use tuples judiciously**
   - Tuples are useful for returning multiple values, but:
   - Consider record types for clarity when the tuple has more than 2-3 elements
   - Example:
     ```fsharp
     // Good for simple cases
     let divrem: BigInteger -> BigInteger -> BigInteger * BigInteger
     
     // Better for more complex returns
     type DivModResult = { Quotient: BigInteger; Remainder: BigInteger }
     let divmod: BigInteger -> BigInteger -> DivModResult
     ```

### Performance Considerations

1. **Use proper data structures**
   - Lists for small collections with frequent prepending
   - Arrays for random access and performance-critical code
   - Sequences for lazy evaluation and potentially infinite collections

2. **Consider tail recursion for recursive functions**
   - Use the `rec` keyword for recursive functions
   - Structure recursive functions to be tail-recursive to avoid stack overflow
   - In F# 8, use the `[<TailCall>]` attribute to explicitly state your intention

3. **Understand memory allocation patterns**
   - Functional programming tends to allocate more small objects
   - Use value types (structs) for small, frequent data structures
   - Consider using `struct` tuples and discriminated unions for performance

## Common Pitfalls and How to Avoid Them

### Compiler Order Dependencies

1. **Definition order matters**
   - F# requires that identifiers are defined before use
   - This includes file order in a project

2. **Solution: Reorder definitions**
   - Move type definitions and functions that are used by others to the top
   - Structure files from most basic/general to most specific
   - Make sure your .fsproj file lists source files in the correct dependency order

### Type Inference Issues

1. **Type inference limitations**
   - F# tries to infer types, but sometimes needs help
   - Common issue: compiler can't determine a type because information is defined later

2. **Solutions:**
   - Add explicit type annotations where needed
   - Reorder code to put "known type" information first
   - Use the pipeline operator to help type inference:
     ```fsharp
     // Instead of (which might not compile):
     List.map (fun x -> x.Length) ["hello"; "world"]
     
     // Use pipeline for better inference:
     ["hello"; "world"] |> List.map (fun x -> x.Length)
     ```

### Function Parameter Handling

1. **Tuple vs. Curried functions confusion**
   - Mixing up tupled and curried function parameters is a common error
   
2. **Solutions:**
   - Be clear about whether a function takes multiple parameters or a tuple
   - Understand the difference:
     ```fsharp
     // Curried function (multiple parameters)
     let add x y = x + y
     let result = add 1 2  // Correct
     let result = add (1, 2)  // Wrong - trying to pass a tuple
     
     // Tupled function (single tuple parameter)
     let addTuple (x, y) = x + y
     let result = addTuple (1, 2)  // Correct
     let result = addTuple 1 2  // Wrong - trying to pass multiple args
     ```

### Mutability and Reference Issues

1. **Assignment operator confusion**
   - Using `=` instead of `<-` for mutable assignments
   
2. **Solutions:**
   - Use `<-` for assignment to mutable values
   - Reserve `=` for equality comparison and initial bindings
     ```fsharp
     let mutable x = 1  // Initial binding uses =
     x <- x + 1  // Assignment uses <-
     ```

3. **Negation operator confusion**
   - Using `!` instead of `not` for boolean negation
   
4. **Solutions:**
   - Use `not` for boolean negation: `not x` instead of `!x`
   - Use `<>` for "not equal" comparisons

### Syntax Errors

1. **Indentation problems**
   - F# is whitespace-sensitive with strict indentation rules
   
2. **Solutions:**
   - Be consistent with indentation
   - Align code blocks properly
   - Use spaces, not tabs (F# doesn't allow tabs)

3. **Parentheses usage**
   - Unnecessary or incorrect use of parentheses
   
4. **Solutions:**
   - Remember that whitespace is the standard separator for function parameters
   - Only use parentheses when needed for precedence or clarity

## Common Compilation Errors

### FS0001: Type Mismatch

This is one of the most common F# errors and occurs when the compiler expected one type but found another.

**Common causes:**

1. **Mixing numeric types**
   ```fsharp
   // Error: int and float types don't mix automatically
   let sum = 1 + 2.0
   ```
   **Fix:** Use explicit conversion
   ```fsharp
   let sum = 1 + int 2.0  // or
   let sum = float 1 + 2.0
   ```

2. **Tuple shape mismatches**
   ```fsharp
   // Error: tuples of different lengths
   let data = [(1, "str1", 'c'); (2, "str2")]
   ```
   **Fix:** Make tuples consistent
   ```fsharp
   let data = [(1, "str1", 'c'); (2, "str2", 'd')]
   ```

3. **Passing a tuple to a function expecting multiple arguments (or vice versa)**
   ```fsharp
   // Function expecting separate arguments
   let add x y = x + y
   // Error: Passing a tuple
   let result = add (1, 2)
   ```
   **Fix:** Pass the arguments correctly
   ```fsharp
   let result = add 1 2
   ```

### FS0003: This value is not a function

Occurs when trying to apply something that isn't a function or when passing too many arguments.

**Common causes:**

1. **Trying to call a non-function**
   ```fsharp
   let x = 42
   x 10  // Error: x is not a function
   ```

2. **Passing too many arguments**
   ```fsharp
   let addTuple (x, y) = x + y
   addTuple 1 2  // Error: addTuple expects a single tuple
   ```
   **Fix:** Use correct argument form
   ```fsharp
   addTuple (1, 2)
   ```

### FS0025: Incomplete pattern matches

Occurs when pattern matching doesn't cover all possible cases.

**Common causes:**

1. **Missing cases in discriminated union matching**
   ```fsharp
   type Shape = 
       | Circle of float
       | Rectangle of float * float
       | Triangle of float * float * float
   
   let area shape =
       match shape with
       | Circle r -> Math.PI * r * r
       | Rectangle (w, h) -> w * h
       // Error: Missing Triangle case
   ```
   **Fix:** Add all cases or use a wildcard with caution
   ```fsharp
   let area shape =
       match shape with
       | Circle r -> Math.PI * r * r
       | Rectangle (w, h) -> w * h
       | Triangle (a, b, c) -> // add triangle area calculation
   ```

### FS0041: A unique overload could not be determined

Occurs when the compiler can't determine which overloaded method to call.

**Common causes:**

1. **Ambiguous method call**
   ```fsharp
   let streamReader filename = new System.IO.StreamReader(filename)
   // Error: Multiple overloads of StreamReader constructor
   ```
   **Fix:** Add type annotation or use named parameters
   ```fsharp
   let streamReader filename = new System.IO.StreamReader(filename:string)
   // or
   let streamReader filename = new System.IO.StreamReader(path=filename)
   ```

2. **Creating intermediate objects that help with type inference**
   ```fsharp
   let streamReader filename =
       let fileInfo = System.IO.FileInfo(filename)
       new System.IO.StreamReader(fileInfo.FullName)
   ```

### FS0072: Lookup on object of indeterminate type

Occurs when trying to access a property or method on an object whose type hasn't been determined yet.

**Common causes:**

1. **Using properties before the compiler knows the type**
   ```fsharp
   let processLength x = 
       // Error: compiler doesn't know what x is yet
       printfn "Length is %d" x.Length
   ```
   **Fix:** Add type annotation or restructure code
   ```fsharp
   let processLength (x:string) = 
       printfn "Length is %d" x.Length
   ```

2. **Solution using pipeline for better inference**
   ```fsharp
   // Instead of:
   List.map (fun x -> x.Length) ["hello"; "world"]
   
   // Use pipeline:
   ["hello"; "world"] |> List.map (fun x -> x.Length)
   ```

### FS0588: Block following this 'let' is unfinished

Occurs due to incorrect indentation in code blocks.

**Common causes:**

1. **Incorrect indentation**
   ```fsharp
   let f = 
       let x = 1
   x + 1  // Error: indentation is wrong
   ```
   **Fix:** Correct the indentation
   ```fsharp
   let f = 
       let x = 1
       x + 1
   ```

### FS1161: TABs are not allowed

F# doesn't allow tab characters in code.

**Fix:** Configure your editor to convert tabs to spaces.

## .NET 8 Specific Features and Best Practices

### F# 8 Features

1. **Static Classes**
   - F# 8 introduces proper static classes using `[<Sealed>]` and `[<AbstractClass>]` together
   - Compiler warnings help identify invalid usage

2. **Improved Lambda Functions**
   - Short syntax for simple lambda functions
   - More intuitive for simple operations

3. **TailCall Attribute**
   - New `[<TailCall>]` attribute to explicitly mark functions intended to be tail-recursive
   - Helps prevent stack overflow errors by making the intention clear

4. **Improved Type Checking**
   - F# 8 includes graph-based type checking for better performance

### Performance Improvements

1. **Better Support for Trimming**
   - Discriminated unions are now trimmable
   - Anonymous records are now trimmable
   - Code using `printfn "%A"` for trimmed records is now trimmable

2. **Compiler Performance**
   - Incremental builds of large project graphs via Reference assemblies
   - CPU-parallelization of the compiler process

3. **Optimization Best Practices**
   - Use struct tuples and discriminated unions for performance-critical code
   - Take advantage of compiler optimizations for tail-recursive functions

### Interoperability with .NET 8

1. **C# Interoperability**
   - Be aware of F# features that don't translate well to C#:
     - F# optional parameters can be awkward to use from C#
     - F# extension methods may not be usable in C#
     - Consider C# consumers when designing public APIs

2. **Library Design**
   - Follow .NET naming conventions for cross-language libraries
   - Use namespaces for top-level organization (more C#-friendly)
   - Provide XML documentation for better IDE integration

## Advanced F# Patterns and Techniques

### Reactive Programming with F#

F# provides excellent support for reactive programming through both built-in observables and the Reactive Extensions (Rx) library.

1. **F# Observable Module**
   - F# has built-in support for observables in the `Observable` module
   - Enables functional transformations on event streams
   - Combine with F# pipelines for clean, readable code
   - Example:
     ```fsharp
     open System

     // Create a timer as an observable
     let createTimerObservable interval =
         let timer = new System.Timers.Timer(float interval)
         timer.AutoReset <- true
         let event = timer.Elapsed |> Observable.map (fun _ -> DateTime.Now)
         timer.Start()
         event
         
     // Transform and subscribe to the events
     createTimerObservable 1000
     |> Observable.map (fun time -> $"Current time: {time:HH:mm:ss}")
     |> Observable.subscribe (printfn "%s")
     ```

2. **Reactive Extensions (Rx.NET)**
   - Use `FSharp.Control.Reactive` library for F#-friendly Rx operators
   - Provides more powerful operators than built-in observables
   - More composable for complex event handling
   - Example with Rx.NET:
     ```fsharp
     open System
     open FSharp.Control.Reactive
     
     // Create and transform observable
     let keyPressStream =
         Observable.fromEvent<_, _> Console.KeyAvailable
         |> Observable.throttle (TimeSpan.FromMilliseconds 500.0)
         |> Observable.map (fun _ -> Console.ReadKey(true))
         |> Observable.filter (fun key -> key.Key <> ConsoleKey.Escape)
         
     // Subscribe
     keyPressStream
     |> Observable.subscribe (fun key -> printfn "Key pressed: %A" key.KeyChar)
     ```

3. **Best Practices with Rx**
   - Prefer pure functions in transformations
   - Use `Observable.scan` to maintain state instead of mutable variables
   - Properly dispose of subscriptions to prevent memory leaks
   - Split complex event processing into smaller, composable pipelines
   - Consider error handling with `Observable.catch` or `Observable.retry`

4. **Rx vs F# Events**
   - Rx provides richer operators and better composition
   - F# events are simpler for basic scenarios
   - Rx has better support for time-based operations and complex event coordination
   - Choose based on complexity needs and library compatibility

### Domain-Driven Design with F#

F# is exceptionally well-suited for Domain-Driven Design (DDD) due to its strong type system, immutability, and ability to model domain concepts clearly.

1. **Modeling the Domain with Types**
   - Use discriminated unions and records to represent domain concepts precisely
   - Create richer types rather than using primitives (e.g., `CustomerId` instead of `string`)
   - Leverage the type system to encode business rules and prevent illegal states

2. **Value Objects and Entities**
   - Implement value objects as immutable records
   - Use single-case discriminated unions to create wrapped types with domain semantics
   - Example:
     ```fsharp
     // Instead of using primitive types
     type EmailAddress = EmailAddress of string
     type CustomerId = CustomerId of Guid
     type CustomerRating = HighValue | Standard | SendToCollections of dueDate:DateTime
     
     // Rich domain model
     type Customer = {
         Id: CustomerId
         Email: EmailAddress
         Rating: CustomerRating
     }
     ```

3. **Enforcing Invariants**
   - Use private constructors with static factory methods to enforce validation
   - Design types so that invalid states are not representable in the type system
   - Use functions to transform domain objects while maintaining invariants

### Railway Oriented Programming

Railway Oriented Programming (ROP) is a functional approach to error handling that uses a two-track model to elegantly handle success and failure cases.

1. **Core Concepts**
   - Model operations as two-track functions (success/failure)
   - Use the `Result<'Success, 'Error>` type to represent outcomes
   - Chain operations in a pipeline that handles errors automatically

2. **Implementation**
   - Create adapter functions for converting between single-track and two-track functions
   - Use `bind`, `map`, and other combinators to compose operations
   - Example:
     ```fsharp
     type Result<'Success, 'Error> =
         | Success of 'Success
         | Error of 'Error

     // Convert a one-track function to a two-track function
     let map f result =
         match result with
         | Success s -> Success (f s)
         | Error e -> Error e
         
     // Chain two-track functions
     let bind f result =
         match result with
         | Success s -> f s
         | Error e -> Error e
     ```

3. **Benefits for Error Handling**
   - Clear separation of success and error paths
   - Composable error handling without complex nested try/catch
   - Consistent error management across the application

### Computation Expressions

Computation expressions provide a way to create custom syntax for specialized computations, enabling more readable and maintainable code.

1. **Understanding Computation Expressions**
   - Allow you to create domain-specific mini-languages
   - Provide uniform syntax for dealing with specific types of computations
   - Common built-in expressions include `async { }`, `seq { }`, and `task { }`

2. **Creating Custom Computation Expressions**
   - Define a builder class with methods like `Bind`, `Return`, and `ReturnFrom`
   - Use for domain-specific languages that make code more intuitive
   - Example for a validation workflow:
     ```fsharp
     type ValidationBuilder() =
         member _.Bind(result, f) =
             match result with
             | Ok value -> f value
             | Error e -> Error e
         member _.Return(value) = Ok value
         
     let validate = ValidationBuilder()
     
     // Usage
     let validateUser name age =
         validate {
             let! validName = validateName name
             let! validAge = validateAge age
             return { Name = validName; Age = validAge }
         }
     ```

3. **Common Applications**
   - Error handling with `Result` type
   - Optional values with `Option` type
   - Asynchronous programming with `Async` or `Task`
   - Custom control flow abstractions for domain-specific languages

### Type Providers

Type providers are a powerful feature that generates types based on external schemas, data sources, or services at compile time.

1. **Core Concept**
   - Generate types on-demand during compilation for external data sources
   - Provide strongly-typed access to data without manual mapping
   - Support various data formats (CSV, JSON, XML, SQL, Web Services)

2. **Common Type Providers**
   - CSV Type Provider for working with tabular data
   - JSON Type Provider for REST APIs and configuration files
   - SQL Type Provider for database access
   - Example:
     ```fsharp
     // Accessing a CSV file with type safety
     open FSharp.Data
     
     type People = CsvProvider<"sample.csv">
     let data = People.Load("people.csv")
     
     for person in data.Rows do
         printfn "%s is %d years old" person.Name person.Age
     ```

3. **Benefits**
   - Compile-time type safety for external data
   - Automatic schema discovery and IntelliSense
   - Reduced boilerplate and mapping code
   - Improved productivity when working with external data sources

### Property-Based Testing

Property-based testing is a testing methodology that focuses on verifying properties of code rather than specific examples.

1. **Core Concepts**
   - Test properties that should always hold true for your functions
   - Generate random test cases to find edge cases automatically
   - Use libraries like FsCheck or Hedgehog for implementation

2. **Defining Properties**
   - Use common patterns like "different paths, same destination" or "there and back again"
   - Express properties as characteristics that should always hold true for your code
   - Example:
     ```fsharp
     // A property: reversing a list twice gives the original list
     let revRevIsOriginal (xs: int list) =
        List.rev (List.rev xs) = xs
        
     // Test with FsCheck
     Check.Quick revRevIsOriginal
     ```

3. **Integration with Testing Frameworks**
   - FsCheck integrates with Expecto, NUnit, xUnit, and other testing frameworks
   - Can be used for both unit tests and integration tests
   - Useful for testing mathematical properties, rounding, partitioning, and refactoring

4. **FsCheck vs Hedgehog**
   - FsCheck is the more established library, based on Haskell's QuickCheck
   - Hedgehog is a newer alternative with integrated shrinking
   - Both use similar functor-based abstractions for generators
   - Example with Hedgehog:
     ```fsharp
     let propReverse = property {
         let! xs = Gen.list (Range.linear 0 100) Gen.alpha
         return xs |> List.rev |> List.rev = xs
     }
     ```

5. **Custom Generators**
   - Create domain-specific generators for realistic test data
   - Use the Gen module for composing complex generators 
   - Example:
     ```fsharp
     // Create a generator for valid email addresses
     let validEmailGen = 
         gen {
             let! username = Gen.nonEmptyString Gen.alphaNum
             let! domain = Gen.nonEmptyString Gen.alphaNum
             return $"{username}@{domain}.com"
         }
     ```

6. **Manual Property Testing**
   - If you prefer not to use a library, you can write simple property test functions
   - Generate your own test cases with random values
   - Track and report failures manually
   - Less powerful but sometimes simpler for basic needs

### Testing Frameworks

F# has several excellent testing frameworks with different approaches to writing and organizing tests.

1. **Main Testing Frameworks**
   - Expecto: A F#-focused testing framework with comprehensive features
   - FsUnit: Adds a fluent assertion style to common test frameworks
   - Unquote: Provides detailed assertion failures using F# quotations
   - Standard .NET frameworks (NUnit, xUnit) also work well with F#

2. **Expecto Features**
   - Tests are simple functions, making them first-class citizens
   - Built-in support for property-based testing with FsCheck
   - Performance testing capabilities
   - Example:
     ```fsharp
     open Expecto
     
     let tests = testList "My tests" [
         test "Addition works" {
             Expect.equal (1 + 1) 2 "Addition should work"
         }
         
         testProperty "Reversing twice is identity" <| fun (xs: int list) ->
             List.rev (List.rev xs) = xs
     ]
     
     [<EntryPoint>]
     let main argv = runTests defaultConfig tests
     ```

3. **Testing Best Practices**
   - Test API responses for correctness and compatibility with contracts
   - Think in terms of functions rather than test frameworks
   - Use property-based testing for mathematical operations, rounding, and business rules
   - Combine unit tests with property-based tests for comprehensive coverage

## Tooling Tips

1. **IDE Support**
   - Use Ionide for VS Code, Visual Studio, or JetBrains Rider for F# development
   - Take advantage of F# Interactive (FSI) for exploratory programming

2. **Debugging F# Code**
   - F# emphasizes compile-time correctness over runtime debugging
   - Use the type system to catch errors at compile time
   - Learn to interpret F# error messages effectively
   - Use FSI to test small code segments

3. **Project Structure**
   - Organize files from general/basic to specific
   - Keep file dependencies in mind when ordering files in the project
   - Consider using a build system like FAKE for complex projects

4. **Continuous Integration**
   - Set up proper CI/CD pipelines for F# projects
   - Include type checking and tests in your CI pipeline

---

This guide provides a starting point for writing better F# code with .NET 8. Remember that F#'s type system and functional-first approach encourage a different mindset than imperative languages. Embrace these differences to write more robust, concise, and maintainable code.
