using Microsoft.EntityFrameworkCore.Design;

namespace TaleScribe.Context;

public class AppDbContextFactory: IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        return new AppDbContext();
    }
}