var sqlConnectionString = "Server=91.239.206.123;Port=17715;Database=bsh;Uid=root;Pwd=_nS12021989;";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDBContext>(options => options.UseMySQL(sqlConnectionString));
builder.Services.AddControllersWithViews();
builder.Services.AddTransient<BshService>();

var app = builder.Build();

app.UseStaticFiles();

app.MapControllerRoute(name: "default",
	defaults: new {controller = "Main"},
	pattern: "{action=Index}"
);

app.Run();