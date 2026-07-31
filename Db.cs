public class ApplicationDBContext: DbContext
{
    public DbSet<Part> parts { get; set; } = null!;
	public DbSet<Model> models { get; set; } = null!;
	public DbSet<Video> videos { get; set; } = null!;
	
	public ApplicationDBContext(DbContextOptions<ApplicationDBContext> options): base(options) {}
}