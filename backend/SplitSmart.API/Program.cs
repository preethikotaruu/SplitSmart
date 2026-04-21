using System.Linq;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
int nextGroupId = 1;
List<Group> groups = new List<Group>();

int nextExpenseId = 1;
List<Expense> expenses = new List<Expense>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.MapPost("/groups", (Group group) =>
{
    group.Id = nextGroupId;
    nextGroupId++;

    groups.Add(group);

    return Results.Ok(new
    {
        message = $"Group '{group.Name}' created successfully",
        data=group    
    });
});
app.MapGet("/groups", () =>
{
    return Results.Ok(groups);
});



app.MapGet("/groups/{id}", (int id) =>
{
    var group = groups.FirstOrDefault(g => g.Id == id);

    if (group == null)
        return Results.NotFound("Group not found ❌");

    return Results.Ok(group);
});

app.MapPut("/groups/{id}", (int id, Group updatedGroup) =>
{
    var group = groups.FirstOrDefault(g => g.Id == id);

    if (group == null)
        return Results.NotFound("Group not found ❌");

    group.Name = updatedGroup.Name;

    return Results.Ok(group);
});

app.MapDelete("/groups/{id}", (int id) =>
{
    var group = groups.FirstOrDefault(g => g.Id == id);

    if (group == null)
        return Results.NotFound("Group not found ❌");

    groups.Remove(group);

    return Results.Ok("Group deleted successfully 🗑️");
});

app.MapPost("/expenses", (Expense expense) =>
{
    var groupExists = groups.Any(g => g.Id == expense.GroupId);

    if (!groupExists)
        return Results.NotFound("Group not found ❌");

    expense.Id = nextExpenseId++;
    expenses.Add(expense);

    return Results.Ok(new
    {
        message = "Expense added successfully 🎉",
        data = expense
    });
});

app.MapGet("/groups/{groupId}/expenses", (int groupId) =>
{
    var groupExpenses = expenses.Where(e => e.GroupId == groupId).ToList();
    return Results.Ok(groupExpenses);
});


app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
public class Group
{
    public int Id { get; set; }
   public string Name { get; set; } = string.Empty;
}
public class Expense
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaidBy { get; set; } = string.Empty;
}