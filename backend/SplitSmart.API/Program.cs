using System.Linq;
var builder = WebApplication.CreateBuilder(args);//configure the webapp

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer(); //“Find all my APIs”
builder.Services.AddSwaggerGen(); //“Show those APIs in Swagger UI”

var app = builder.Build(); //“Take everything we configured in builder and create the actual running app”
int nextGroupId = 1;
List<Group> groups = new List<Group>();

int nextExpenseId = 1;
List<Expense> expenses = new List<Expense>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) //Only enable Swagger in development (local machine), not in production”
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

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
    if (string.IsNullOrWhiteSpace(expense.Title))
        return Results.BadRequest("Title is required ❌");

    if (expense.Amount <= 0)
        return Results.BadRequest("Amount must be greater than 0 ❌");

    if (string.IsNullOrWhiteSpace(expense.PaidBy))
        return Results.BadRequest("PaidBy is required ❌");

    if (expense.SplitAmong == null || expense.SplitAmong.Count == 0)
        return Results.BadRequest("SplitAmong must have at least one person ❌");

    var group = groups.FirstOrDefault(g => g.Id == expense.GroupId);

    if (group == null)
        return Results.NotFound("Group not found ❌");

    if (!group.Members.Contains(expense.PaidBy))
        return Results.BadRequest("PaidBy must be a valid group member ❌");

    var invalidMembers = expense.SplitAmong
        .Where(person => !group.Members.Contains(person))
        .ToList();

    if (invalidMembers.Any())
        return Results.BadRequest($"These people are not in the group: {string.Join(", ", invalidMembers)} ❌");

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

app.MapGet("/groups/{groupId}/balances", (int groupId) =>
{
    var groupExpenses = expenses.Where(e => e.GroupId == groupId).ToList();

    if (!groupExpenses.Any())
        return Results.Ok(new List<BalanceResult>());

    var balances = new Dictionary<string, decimal>();

    foreach (var expense in groupExpenses)
    {
        if (expense.SplitAmong == null || expense.SplitAmong.Count == 0)
            continue;

        var share = expense.Amount / expense.SplitAmong.Count;

        if (!balances.ContainsKey(expense.PaidBy))
            balances[expense.PaidBy] = 0;

        balances[expense.PaidBy] += expense.Amount;

        foreach (var person in expense.SplitAmong)
        {
            if (!balances.ContainsKey(person))
                balances[person] = 0;

            balances[person] -= share;
        }
    }

    var result = balances.Select(b => new BalanceResult
    {
        Person = b.Key,
        NetBalance = b.Value
    }).ToList();

    return Results.Ok(result);
});

app.MapGet("/groups/{groupId}/settlements", (int groupId) =>
{
    var groupExpenses = expenses.Where(e => e.GroupId == groupId).ToList();

    if (!groupExpenses.Any())
        return Results.Ok(new List<SettlementResult>());

    var balances = new Dictionary<string, decimal>();

    foreach (var expense in groupExpenses)
    {
        if (expense.SplitAmong == null || expense.SplitAmong.Count == 0)
            continue;

        var share = expense.Amount / expense.SplitAmong.Count;

        if (!balances.ContainsKey(expense.PaidBy))
            balances[expense.PaidBy] = 0;

        balances[expense.PaidBy] += expense.Amount;

        foreach (var person in expense.SplitAmong)
        {
            if (!balances.ContainsKey(person))
                balances[person] = 0;

            balances[person] -= share;
        }
    }

    var creditors = balances
        .Where(b => b.Value > 0)
        .Select(b => new { Person = b.Key, Amount = b.Value })
        .ToList();

    var debtors = balances
        .Where(b => b.Value < 0)
        .Select(b => new { Person = b.Key, Amount = Math.Abs(b.Value) })
        .ToList();

    var settlements = new List<SettlementResult>();

    int i = 0, j = 0;

    while (i < debtors.Count && j < creditors.Count)
    {
        var debtor = debtors[i];
        var creditor = creditors[j];

        var settleAmount = Math.Min(debtor.Amount, creditor.Amount);

        settlements.Add(new SettlementResult
        {
            FromPerson = debtor.Person,
            ToPerson = creditor.Person,
            Amount = settleAmount
        });

        debtor = new { debtor.Person, Amount = debtor.Amount - settleAmount };
        creditor = new { creditor.Person, Amount = creditor.Amount - settleAmount };

        debtors[i] = debtor;
        creditors[j] = creditor;

        if (debtors[i].Amount == 0)
            i++;

        if (creditors[j].Amount == 0)
            j++;
    }

    return Results.Ok(settlements);
});
app.MapPost("/groups/{id}/members", (int id, List<string> members) =>
{
    var group = groups.FirstOrDefault(g => g.Id == id);

    if (group == null)
        return Results.NotFound("Group not found ❌");

    foreach (var member in members)
    {
        if (!group.Members.Contains(member))
        {
            group.Members.Add(member);
        }
    }

    return Results.Ok(new
    {
        message = "Members added successfully 🎉",
        data = group
    });
});
app.MapGet("/groups/{id}/members", (int id) =>
{
    var group = groups.FirstOrDefault(g => g.Id == id);

    if (group == null)
        return Results.NotFound("Group not found ❌");

    return Results.Ok(group.Members);
});

app.Run();

public class Group
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Members { get; set; } = new List<string>();
}
public class Expense
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaidBy { get; set; } = string.Empty;
    public List<string> SplitAmong { get; set; } = new List<string>();
}

public class BalanceResult
{
    public string Person { get; set; } = string.Empty;
    public decimal NetBalance { get; set; }
}

public class SettlementResult
{
    public string FromPerson { get; set; } = string.Empty;
    public string ToPerson { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}