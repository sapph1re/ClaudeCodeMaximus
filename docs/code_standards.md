# Coding Standards and Practices

1. Avoid "magic numbers" and "magic strings" in the implementation part of the code when a literal or number has a meaning outside of the code block it's being used in. Use Costants.cs file and create sections in there when necessary. The goal here is to increase visibility of the hardcoded literals and numbers in order to prevent duplication and misspelling of these accross the code.
	* For already existing Constants.cs try to follow the existing structure. Expand if necessary.
	* When you need to create sections in Constants.cs or entirely new Constants.cs prefer to create sections based on a constant's roles than modules these are used in. E.g.: 
		DatabaseTimeout and HubSpotTimeout go to Timeouts section, while MaxDatabaseConnections, HubSpotMinThreads and HubspotMaxThreads go to Limits section. Do not create Database section and HubSpot section just because these constants are used in these modules.
	* When following existing sections do follow even if these are designed by modules the constants are used in. E.g. if you see Database section in Constants.cs, put DatabaseTimeout there.
	Example: ❌ Attributes in Poco classes like EFCore entities like maximum length of a string property only have a meaning within the type they're in. It's perfectly fine to leave them and not move to Constants. 
	Example: ✓ Schedule frequency (Day/Week/Month) will always have a meaning outside of the code it's used in, just because it's a common knowledge. Always aim to increase visibility of such things by either declaring them in Constants or by creating a dedicated Enum like ScheduleFrequency or a value type with well known semantics (see DatabaseName for example)
2. Always follow "single source of truth" principle: there should only be one module responsinble for serving a specific data objects to the application from a persistent storage or API, and one module these objects are consumed by for persistence or sending to an API. E.g.:
	* Database operations for a specific ERCore entity should be handled by a single repository class, and this class should be the only one to interact with the database for this entity. Other modules should not interact with the database directly for this entity, but should use the repository class instead.
	* HubSpot API class or set of classes are the only ones to interact with HubSpot API. Other modules should not interact with HubSpot API directly, but should use the HubSpot API class or classes instead.
3. 
4. Always preferer `nameof(fieldname)` over `"fieldname"` when it's necessary to refer to some field name via string literal

## Code Attribution
**Rule:** Mark AI-generated classes in `<remarks>` section
```csharp
/// <remarks>Created by Claude</remarks>
```

## Code Reuse Priority
1. ✓ Reuse existing non-AI classes (highest priority)
2. ✓ Reuse AI-generated classes (when no alternative)
3. ✓ Create new class (only when no reusable option exists)

**Pattern matching:** Analyze existing solutions for similar challenges → replicate approach

## Project Initialization
❌ Remove all template example code (WeatherService, counter demos, etc.)

## SQL Queries
- Extract to Constants.SqlQueries.cs which is a partial static class `Constants` with public nested class `SqlQueries`. 

## Static Members
❌ Avoid static methods and properties (harms testability/maintainability)
✓ Use instance methods and properties

## Single Source of Truth

**Pattern:** One module owns each data type's persistence/API interaction

```
✓ Repository pattern: One repository per entity → only it accesses DB for that entity
✓ API facade: Dedicated class(es) for external APIs → only they call the API
❌ Direct database/API calls from multiple modules
```

## File Organization

**Structure:** Follow existing project conventions
- Check for Models/Model or Controller/Controllers or ViewModel/ViewModels folder before creating new structure

### ONE FILE = ONE TYPE (Critical Rule)

**NEVER put multiple top-level types in a single file.** Each file must contain exactly ONE:
- class
- enum
- struct
- record
- interface

File name MUST match the type name: `MyClass.cs` contains `class MyClass`, `MyEnum.cs` contains `enum MyEnum`.

```csharp
// ❌ WRONG - Multiple types in one file (MyService.cs)
public class MyService { ... }
public enum MyServiceStatus { Active, Inactive }  // NO! This belongs in MyServiceStatus.cs

// ❌ WRONG - Enum at bottom of class file
public class Order { ... }
public enum OrderStatus { Pending, Complete }  // NO! Create OrderStatus.cs

// ❌ WRONG - Helper class in same file
public class PaymentProcessor { ... }
public class PaymentResult { ... }  // NO! Create PaymentResult.cs

// ✓ CORRECT - Each type in its own file
// MyService.cs
public class MyService { ... }

// MyServiceStatus.cs (separate file)
public enum MyServiceStatus { Active, Inactive }
```

**Why this matters:**
1. Team members can't find types when they're hidden inside other files
2. Git history becomes meaningless when unrelated types share a file
3. Code navigation tools work poorly with multi-type files
4. It's unprofessional and creates maintenance nightmares

**Exception - Private nested types:** Nested types that are ONLY used within their parent class may stay in the parent's file, but:
- If >12 lines → extract to `ParentClass.NestedType.cs` using `partial class`
- If used by ANY other type → extract to its own file immediately

## Naming Conventions (MVC/MVVM)
```
Domain models:     *Model
View models:       *ViewModel
```

## DI/IoC lifetime scopes
**Never** increase lifecycle scope of a module without an explicit instruction from human supervisor. Example:
❌ Do not change services.AddTransient<...>() to services.AddScoped<...>()
❌ Do not change services.AddScoped<...>() to services.AddSingleton
✓ Feel free to change services.AddScoped<...>() to services.AddTransient<...>()

Always warn human supervisor when creating a new Scoped or Singleton registration and explain your reasoning why it absolutely must be so. If you can reason enough - create Transient registration instead.

## Data Transformation Pattern

**Rule:** Three-layer transformation chain (Database Entity → Model → ViewModel)

### Transformation Strategy (Priority Order)

**Primary:** Constructor-based mapping (source-aware classes)
```csharp
var model = new Model(entity);        // ✓ Preferred
var model = new Model(inputDto);      // ✓ Preferred
var viewModel = new ViewModel(model); // ✓ Preferred
```

**Fallback:** Extension methods (when constructor creates unwanted dependency/complexity)
```csharp
var model = inputDto.ToModel();       // ✓ When type resolution blocked
var viewModel = model.ToViewModel();  // ✓ When circular dependency risk
```
- Extension method location: Static classes named `{Type}Extensions` (e.g., `ModelExtensions`, `ViewModelExtensions`)
- Trigger: Constructor would create cross-project dependency or architectural violation

**❌ Generally avoid:** Using AutoMapper even when it's present in the solution. 
Reasoning: It hinders visibility of the transformation, and looking for mapper profiles can sometimes be tricky for human developers and supervisors.
Notes: Automapper in inherent mode walks all the properties of object, which causes corresponding code execution. Might cause EFCore lazy loading to fire.

### Prohibited Patterns
❌ Database entities in output responses  
❌ Input DTOs in business logic  
❌ Models in output responses

### Required Flow
```csharp
// Database → Output
var entity = context.Get(id);
var viewModel = new ViewModel(entity);  // or entity.ToViewModel()
return viewModel;

// Input → Logic  
var model = new Model(inputDto);  // or inputDto.ToModel()
Process(model);

// Logic → Output
var viewModel = new ViewModel(model);  // or model.ToViewModel()
return viewModel;
```

### Invariant
Entities, Models, and ViewModels never share operational context without explicit mapping.

## Methods return types
❌ Do not design methods returning `Dictionary<string, object>` as most of the time it means you're intending to return a structured object.
✓ Design methods returning proper Models or ViewModels, use private nested types when returned object only has a meaning within method's declaring type.

### Serialized Output (Controllers, API responses, JSON)
❌ **NEVER** use anonymous types or tuples as return types from controllers or any methods that produce serialized output (JSON, XML, etc.)
```csharp
// ❌ WRONG - Anonymous type
return Ok(new { Id = 1, Name = "Test" });

// ❌ WRONG - Tuple
return Ok((Id: 1, Name: "Test"));

// ❌ WRONG - ValueTuple in method signature
public (int Id, string Name) GetData() => ...
```

✓ **ALWAYS** use properly defined response models:
```csharp
// ✓ CORRECT - Explicit response model
return Ok(new MyResponseModel { Id = 1, Name = "Test" });
```

**Reasoning:** Anonymous types and tuples in serialized output:
1. Make response format hard to control and document
2. Hide the actual contract from API consumers
3. Create maintenance nightmares when changes are needed
4. Cannot be referenced by tests or client code
5. Are simply ugly and unprofessional in production APIs

## Refactoring
Preserve methods, variables and classes order as you refactor or split the code.

## Code duplication avoidance (DRY principle)
Aim for no code duplication, whenever there is an intent to create a segment of code based on a copy of another segment attempt to reference the origin code block instead. 
If that can’t be done without modifying code’s public signature stop and ask supervising human for a permission to refactor. Explain the reasons and your approach. 

# Default Code style

When no code styles defined for the project please follow these rules:
* ❌ Don't use `#region` directives, a need for a collapsible code region indicates a class is too complex and needs refactoring.
* Use tabs for indentation, set tab size to 4 spaces. Never use spaces for indentation.
* ✓ in C# **always** use `nameof()` where possible instead of string value, when referring to a field,property or a method:
✓ Do this:
```csharp
@Url.Route(nameof(MyNamespace.Controllers.MyTestController.Index), "MyTest")
```
❌ Do not do this:
```csharp
@Url.Route("Index", "MyTest")
```
* Use spaces to align the code, i.e. when you attempt for it to look nicer.
* For single line blocks (e.g. single line if, for, while etc) do not use curly braces unless it's a `lock` statement.
* When you check for conditions in a method that should be funfilled in order for it to be executed, prefer returning early instead of nesting the main logic of the method in an if statement. E.g.:
✓ Do this:
```csharp
if (!conditionMet)
	return;
```
❌ Do not do this:
```csharp
if (conditionMet)
{
// main logic here
}
```
## Unicode icon symbols
C#, JavaScript and pipelines: don’t use unicode icon symbols
HTML markup: escape unicode icon symbols 
MD: Use icon symbols as it but keep it reasonable