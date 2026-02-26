using KanbanApi.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KanbanApi.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Board> Boards { get; set; }
    public DbSet<Column> Columns { get; set; }
    public DbSet<Card> Cards { get; set; }
    public DbSet<BoardMember> BoardMembers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);


        modelBuilder.Entity<BoardMember>(entity =>
        {
            entity.HasKey(bm => new { bm.UserId, bm.BoardId });

            entity.HasOne(bm => bm.User)
                .WithMany(u => u.BoardMemberships)
                .HasForeignKey(bm => bm.UserId);

            entity.HasOne(bm => bm.Board)
                .WithMany(b => b.Members)
                .HasForeignKey(bm => bm.BoardId);
        });

        modelBuilder.Entity<Board>(entity =>
        {
            entity.HasMany(b => b.Columns)
                .WithOne(c => c.Board)
                .HasForeignKey(c => c.BoardId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(b => b.Owner)
                .WithMany(u => u.OwnedBoards)
                .HasForeignKey(m => m.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

        });
        
        modelBuilder.Entity<Column>(entity =>
        {
            entity.HasMany(c => c.Cards)
                .WithOne(c => c.Column)
                .HasForeignKey(card => card.ColumnId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
