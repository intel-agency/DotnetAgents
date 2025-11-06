using DotnetAgents.Core.Models;

using Microsoft.EntityFrameworkCore;

using System.Collections.Generic;
using System.Reflection.Emit;

namespace DotnetAgents.AgentApi.Data
{
    public class AgentDbContext : DbContext
    {
        public AgentDbContext(DbContextOptions<AgentDbContext> options)
            : base(options)
        {
        }

        public DbSet<AgentTask> AgentTasks { get; set; }

        // This is a good place to configure your Status enum conversion
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AgentTask>()
                .Property(t => t.Status)
                .HasConversion<string>(); // Stores the enum as a string in the DB
        }
    }
}