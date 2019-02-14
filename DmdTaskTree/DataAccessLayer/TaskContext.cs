using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace DmdTaskTree.DataAccessLayer
{
    public class TaskContext : DbContext
    {
        public DbSet<TaskNote> TaskNotes { get; set; }
        public DbSet<TaskTreeNode> TaskTreeNodes { get; set; }

        public TaskContext(DbContextOptions<TaskContext> options) : base(options)
        {
            Database.EnsureCreated();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TaskTreeNode>().HasOne(node => node.Ancestor)
                .WithMany(anc => anc.TaskTreeNodes)
                .HasForeignKey(node => node.AncestorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TaskTreeNode>().HasOne(node => node.Descendat)
                .WithOne(g => g.TaskTreeNode)
                .HasForeignKey<TaskTreeNode>(node => node.DescendantId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}