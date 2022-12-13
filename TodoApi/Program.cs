using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Overwrite the existing Logging provider and add Console for output
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registering/Injecting the 'ItemRepository' to our builder services
builder.Services.AddSingleton<ItemRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

#region .NET 7 New Feature #1 - Grouping
// Creating a group increases URL readability and enforces uniformity 
// Makes routing dynamic and easy to update/manage routes
var items = app.MapGroup("/todoItems");

#endregion

#region Setting up the endpoints

// GET - All Items
items.MapGet("/", ([FromServices] ItemRepository items) => {
    return items.GetAll();
});

// GET - All Item by Id
items.MapGet("/{id}", ([FromServices] ItemRepository items, int id) => {
    return items.GetById(id);
});

// POST - Add a new Item
items.MapPost("/", ([FromServices] ItemRepository items, Item newItem) => {
    // Check if the entry by this Id already exists. If no(null), add the item. Else return a BadRequest response to the user.
    if(items.GetById(newItem.id) == null) {
        items.Add(newItem);
        return Results.Created($"/items/{newItem.id}", newItem);
    }

    return Results.BadRequest();
});

// PUT - Update an item by Id
items.MapPut("/{id}", ([FromServices] ItemRepository items, int id, Item item) => {
    // Check if item doesn't exist
    if(items.GetById(item.id) == null)
        return Results.BadRequest();
    
    items.Update(item);
    return Results.NoContent(); // No need for any return 
});

// DELETE - Remove an item by Id
items.MapDelete("/{id}", ([FromServices] ItemRepository items, int id) => {
    // Check if item doesn't exist
    if(items.GetById(id) == null)
        return Results.BadRequest();
    
    items.Delete(id);
    return Results.NoContent(); // No need for any return 
});

#endregion

#region .NET 7 New Feature #2 - Filters (Pre/Post-Processing)

//Add pre-processing and post-processing logic to an handler
items.MapGet("/filters", (string secretPasscode) => {
    return "Hello World";
})
.AddEndpointFilter(async (context, next) => {
    if(!context.HttpContext.Request.QueryString.Value.Contains("meep")) 
    {
        return Results.BadRequest();
    }

    return await next(context);
});

// Could add multiple filters (Filter Chaining) to a handler as well.
// Filter code called before the next() -> executed in order of First In, First Out (FIFO) order.
// Filter code called after next() -> executed in order of Last In, First Out (LIFO) order.
app.MapGet("/filterV2", () =>
{
    app.Logger.LogInformation("             Endpoint");
    return "Test of multiple filters";
})
.AddEndpointFilter(async (efiContext, next) =>
{
    app.Logger.LogInformation("Before first filter");
    var result = await next(efiContext);
    app.Logger.LogInformation("After first filter");
    return result;
})
.AddEndpointFilter(async (efiContext, next) =>
{
    app.Logger.LogInformation(" Before 2nd filter");
    var result = await next(efiContext);
    app.Logger.LogInformation(" After 2nd filter");
    return result;
})
.AddEndpointFilter(async (efiContext, next) =>
{
    app.Logger.LogInformation("     Before 3rd filter");
    var result = await next(efiContext);
    app.Logger.LogInformation("     After 3rd filter");
    return result;
});

#endregion

#region .NET 7 New Feature #3 - File Uploads

// Bind files to the handler methods with ease
app.MapPost("/upload", async (IFormFile file) => {
    await file.CopyToAsync(File.OpenWrite("upload.txt"));
});

#endregion

#region .NET 7 New Feature #4 - Array Binding

// Append multiple values into the Querystring
// Minimal API would take care of assembling it into a string array for us
// Resulting request URL [created by Minimal API automatically] - http://localhost:5095/array-binding?names=meep&names=morp&names=zeep
app.MapGet("/array-binding", (string[] names) => {
    return $"name1: {names[0]}, name2: {names[1]}";
});

#endregion

#region .NET 7 New Feature #5 - Parameter Property Binding

// Binding request data with [AsParameters] to bind Models into our request
// Result request URL - http://localhost:5095/todos?Id=4&Title=Test%20task&Completed=false
app.MapPost("/todos", async ([AsParameters]Todo newTodo) => {
    // Save the Todo
    var test = newTodo;
    app.Logger.LogInformation(JsonSerializer.Serialize(test));
});

#endregion

app.UseHttpsRedirection();

app.Run();

record Item(int id, string title, bool completed);

// Class/Model Declaration
class ItemRepository {
    // Property to hold a list of 'Item' type
    private readonly Dictionary<int, Item> _items = new Dictionary<int, Item>();

    public ItemRepository()
    {
        // Creating a few instances of 'Item' type
        var item1 = new Item(1, "Go to the Gym", false);
        var item2 = new Item(2, "Go to the supermarket", false);
        var item3 = new Item(3, "Finish the essay", false);

        // Adding the items to the property (Dictionary)
        _items.Add(item1.id, item1);
        _items.Add(item2.id, item2);
        _items.Add(item3.id, item3);
    }

    // CRUD Operations Declaration and Definition

    // Get All Operation
    public List<Item> GetAll() => _items.Values.ToList();

    // Get specific Item value
    public Item? GetById(int id) => _items.ContainsKey(id) ? _items[id] : null;    

    // Adding a new Item to the dictionary
    public void Add(Item obj) => _items.Add(obj.id, obj);

    // Updating an existing item
    public void Update(Item obj) => _items[obj.id] = obj;

    // Deleting an existing item
    public void Delete(int id) => _items.Remove(id);
}

// For Feature #5 - Parameter Property Binding
class Todo {
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool Completed { get; set; }
}