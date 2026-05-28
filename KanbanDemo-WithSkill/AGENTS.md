# KanbanDemo

| Setting | Value |
|---------|-------|
| **Interactivity Mode** | Server |
| **Interactivity Scope** | Per-page |

## Rendering configuration
This project uses per-page Interactive Server with prerendering.
Created with `dotnet new blazor -int Server`.

Pages are static SSR by default. Only components that explicitly add `@rendermode InteractiveServer` become interactive.

## Adding new components
- Create new `.razor` files in `Components/Pages/` for routable pages or `Components/` for shared components.
- New pages are static SSR by default. Only add `@rendermode InteractiveServer` to components that need client-side behavior (live search, real-time updates, complex form interactions).
- Static pages can use standard HTML forms with `[SupplyParameterFromForm]` — no interactivity needed.

## Data access
- Components can inject services directly — EF Core DbContext, file system, server-only APIs. No HTTP API layer needed.

## Available services

The following service is registered in DI as a singleton:

```csharp
public record TaskItem(int Id, string Title, string Assignee, string Priority, DateTime DueDate, string Status);

public interface ITaskService
{
    Task<List<TaskItem>> GetAllTasksAsync();
    Task<TaskItem> CreateTaskAsync(string title, string assignee, string priority, DateTime dueDate);
    Task UpdateTaskStatusAsync(int taskId, string newStatus);
}
```

Statuses are: "To Do", "In Progress", "Done".

## Environment constraints
- Interactive components run on the server via SignalR. `HttpContext` is available in static components but NOT in interactive components during the SignalR circuit lifetime.
- Static pages can access `HttpContext` via `[CascadingParameter]`.
- Browser APIs are not directly available — use `IJSRuntime` interop in interactive components.

## Don'ts
- Don't add `@rendermode InteractiveServer` to every page — keep read-only content static for performance and lower server memory.
- Don't inject `HttpContext` in interactive components — it's not available during SignalR circuit lifetime.
- Don't set `@rendermode` on `<Routes>` in `App.razor` — that makes it global. Per-page mode means individual components opt in.

---

# Skill: Plan a Blazor UI Change

When asked to build a complex UI feature, **plan the component decomposition before writing any code**. A single monolithic page component is almost never the right answer — break the UI into focused, composable components.

## Planning Workflow

### Step 1 — Map the Visual Regions

Read the request and identify every distinct visual region. Each region that has its own data, behavior, or layout responsibility is a candidate component.

Draw the component tree:

```
InventoryDashboard          (page — owns data, orchestrates layout)
├── StockSummaryBar         (read-only stats: total items, low-stock count, value)
├── InventoryFilters        (search box, category dropdown, stock-level toggle)
├── InventoryTable          (sortable table of products)
│   └── InventoryRow        (single product row with inline edit/delete)
└── AddProductForm          (slide-out form for new products)
```

Rules for identifying components:
- **Distinct responsibility** — a region owns its own state or behavior → separate component
- **Repeated structure** — items in a list, cards in a grid → extract the item template
- **Independent interactivity** — a section that handles user input separately from its siblings → separate component
- **Size** — any section that would exceed ~150 lines of markup on its own → split it

### Step 2 — Classify Each Component

For every component in the tree, determine:

| Component | Action | Render Mode | State Owned | Lines (est.) |
|-----------|--------|-------------|-------------|-------------|
| InventoryDashboard | Create | InteractiveServer | product list, filter state | ~80 |
| StockSummaryBar | Create | (inherits) | none — receives data | ~30 |
| InventoryFilters | Create | (inherits) | search text, selected category | ~60 |
| InventoryTable | Create | (inherits) | sort column, sort direction | ~50 |
| InventoryRow | Create | (inherits) | inline-edit mode flag | ~60 |
| AddProductForm | Create | (inherits) | form model | ~80 |

**A page component that exceeds ~200 lines of combined markup + code is too large.** If your estimate puts a single component above that, split further.

### Step 3 — Design Data Flow

Identify the **state owner** for each piece of data, then map how it flows:

```
InventoryDashboard (owns: products[], filters)
  │
  ├─ [Parameter] products ──→ StockSummaryBar (reads aggregate stats)
  │
  ├─ [Parameter] filters ──→ InventoryFilters
  │   └─ EventCallback<Filters> OnFiltersChanged ──→ InventoryDashboard
  │
  ├─ [Parameter] filteredProducts ──→ InventoryTable
  │   └─ [Parameter] product ──→ InventoryRow
  │       ├─ EventCallback<Product> OnSave ──→ InventoryTable ──→ InventoryDashboard
  │       └─ EventCallback<Product> OnDelete ──→ InventoryTable ──→ InventoryDashboard
  │
  └─ EventCallback<Product> OnProductAdded ←── AddProductForm
```

Rules:
- Data always flows **down** through `[Parameter]`
- Events always flow **up** through `EventCallback<T>`
- The page/parent **owns the data** and passes filtered/transformed views to children
- Children **never mutate parameters** — they notify the parent via callbacks
- If data must cross more than 2 levels without intermediate components needing it, use a cascading value or a scoped service

### Step 4 — Identify Reuse Opportunities

Before creating a new component, check if an existing component in the project can serve the purpose. Look for:
- Existing list-item components that match the structure
- Shared filter/search components already in the project
- Generic components (e.g., `DataTable<T>`, `Pagination`) that accept templates

If a component will be used in more than one page, place it in a `Shared/` or `Components/` folder.

### Step 5 — Order the Implementation

Build bottom-up — leaf components first, then parents that compose them:

1. **Models/DTOs** — define the data shapes
2. **Services** — data access, business logic (interface + implementation)
3. **Leaf components** — components with no children (InventoryRow, StockSummaryBar)
4. **Container components** — components that compose leaves (InventoryTable, InventoryFilters)
5. **Page component** — wires everything together, registers routes
6. **Configuration** — DI registration, render mode setup

Each component should be independently compilable. Never reference a component that doesn't exist yet.

## Output Format

Before writing any code, output a plan in this format:

```markdown
## Component Plan: [Feature Name]

### Component Tree
[ASCII tree showing parent-child relationships]

### Component Table
| Component | Action | Render Mode | Purpose | Est. Lines |
|-----------|--------|-------------|---------|------------|
| ... | ... | ... | ... | ... |

### Data Flow
[State owner] → [Parameters down] → [EventCallbacks up]

### Implementation Order
1. [First file to create — why]
2. [Second file — why]
...
```

## Anti-Patterns to Avoid

| Anti-Pattern | Why It's Wrong | Correct Approach |
|-------------|----------------|-----------------|
| One page component with 500+ lines | Impossible to test, reuse, or maintain | Decompose into focused components |
| Passing 10+ parameters through intermediate components | Parameter drilling obscures intent | Use cascading values or a scoped state service |
| Child component fetching its own data from an API | Multiple components making redundant calls | Parent owns data, passes via parameters |
| Inline rendering of list items with complex markup | Duplicated logic, no reuse, hard to test | Extract item template into its own component |
| Building everything in one file then "refactoring later" | Refactoring rarely happens; the monolith ships | Plan the decomposition upfront |
| Generic components for one-off usage | Over-engineering adds complexity | Only extract generics when reuse is proven |

## Guidelines

- **Plan before coding.** Write the component table and data flow map before creating any `.razor` files.
- **Prefer many small components over one large one.** A component with a single clear purpose is easier to understand, test, and reuse.
- **State ownership is the first decision.** Before writing fetch logic, decide which component owns the data.
- **Build bottom-up.** Create leaf components first so parent components can reference them immediately.
- **Name components after what they render**, not what they do internally: `ProductCard` not `ProductRenderer`, `OrderFilters` not `FilterHandler`.

---

# Skill: Author Blazor Component

## Design Rules

- Decompose UI into a component tree mirroring visual structure. Parent orchestrates; children render.
- Data flows **down** via `[Parameter]`. Events flow **up** via `EventCallback`.
- Enumerate all states before writing markup: loading, empty, loaded, error, unauthorized. Handle each with `@if`/`@else`.
- Never mutate `[Parameter]` properties. Copy to a private field in `OnParametersSet`.
- Delegate business logic to injected services. Components are thin UI shells.

### Size Limits

| Metric | Target |
|--------|--------|
| Lines (markup + `@code`) | 100–200; refactor above 500 |
| Cyclomatic complexity | ≤ 10 per method/render block |
| Parameters / event handlers | ≤ 10 each |

### State Handling Pattern

```razor
@if (error is not null)
{
    <div class="alert alert-danger">@error <button @onclick="LoadData">Retry</button></div>
}
else if (items is null)
{
    <p>Loading...</p>
}
else if (items.Count == 0)
{
    <GridEmptyState Message="No records found." />
}
else
{
    <GridBody Items="items" />
}
```

## Parameters

**Do:**
- `[Parameter] public string Title { get; set; } = "";` — public auto-property with `{ get; set; }`.
- `[Parameter, EditorRequired] public string Label { get; set; } = "";` — mark required params.
- `[Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }` — splatting HTML attributes.

**Don't:**
- `required` or `init` on parameters — runtime failures (BL0007).
- Logic in parameter getters/setters.
- Write to parameter properties inside the component.

### Deriving Local State

```csharp
[Parameter] public string InitialText { get; set; } = "";
private string currentText = "";

protected override void OnParametersSet()
{
    currentText = InitialText;
}
```

## EventCallback

Use `EventCallback` / `EventCallback<T>` for parent-child events. Never use `Action` or `Func` — they don't trigger parent re-render.

```csharp
[Parameter] public EventCallback<int> OnAddToCart { get; set; }
```

```razor
<button @onclick="() => OnAddToCart.InvokeAsync(Quantity)">Add</button>
```

## Child Content / RenderFragment

```csharp
// Single slot
[Parameter] public RenderFragment? ChildContent { get; set; }

// Typed template (generic component)
[Parameter] public RenderFragment<TItem>? RowTemplate { get; set; }

// Multiple named slots
[Parameter] public RenderFragment? Header { get; set; }
[Parameter] public RenderFragment? Footer { get; set; }
```

Use `@typeparam TItem` for generic components. Use `@key` on repeated elements in loops.

## Lifecycle

Execution order:
1. `SetParametersAsync` — raw parameter assignment (advanced).
2. `OnInitialized[Async]` — once on first render. Load data here.
3. `OnParametersSet[Async]` — after every parameter update. Copy params to local fields here.
4. `OnAfterRender[Async](bool firstRender)` — after DOM update. JS interop only here.

## Disposal

Implement `IAsyncDisposable` (not `IDisposable`) when the component owns event subscriptions, timers, `CancellationTokenSource`, or JS interop references.

## Async Rules

**Do:** `await` every async operation. Use `InvokeAsync` + `StateHasChanged` for external events.

**Don't:** `.Result`, `.Wait()`, `Task.Run`, `ContinueWith`, `Thread.Start` — these deadlock or escape the sync context.

## Styling

Use CSS isolation (`.razor.css`) for component-scoped styles. Don't use inline `style` attributes — use CSS classes or `data-*` attributes.

## Don'ts Checklist

- `required`/`init` on `[Parameter]` — runtime failure.
- Mutate `[Parameter]` from inside — copy to private field.
- JS interop in `OnInitializedAsync` — use `OnAfterRenderAsync`.
- `Action`/`Func` for event params — use `EventCallback`.
- `Task.Run`/`.Result`/`.Wait()` — deadlock.
- `StateHasChanged` in every handler — unnecessary overhead.
- Inline `style` attributes — use CSS classes.
- Gold-plating: ARIA roles, extra wrapper divs, features the prompt didn't ask for.
