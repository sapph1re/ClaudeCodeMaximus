# Guidelines for AI agents working on this project

## Aim to complete implementation

When given a complex task aim to implement it fully. If you must break it down to parts try making the process evident for human supervisor:
* ❌ Do not create compilable empty/stub/placeholder methods. 
* ❌ Do not use NotImplementedException
* ✓ Make empty/stub/placeholder methods intentionally non-compilable, either by omitting the return value or by placing `#error` directive as below:
``` csharp
#if !SUPPRESS_CUSTOM_ERRORS
    #error This method is not yet implemented
#endif
```
then use `dotnet build -p:DefineConstants="SUPPRESS_CUSTOM_ERRORS"`

## Refactor the code only after implementation
* ❌ Do not attempt to create a perfectly reusable code as you generate it. 
* ✓ Follow basic coding rules as you create the code, keep methods of reasonable length, extract code parts that are obviously being reused into methods and that's it.
* ✓ Once you have finished the implementation then only analyze the code for repeated patterns and options for simplification
