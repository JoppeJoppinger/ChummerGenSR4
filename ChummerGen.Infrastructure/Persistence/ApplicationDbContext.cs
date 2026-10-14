using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChummerGen.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<CharacterEntity> Characters => Set<CharacterEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<CharacterEntity>(entity =>
        {
            entity.HasKey(character => character.Id);
            entity.Property(character => character.Name).HasMaxLength(256).IsRequired();
            entity.Property(character => character.OwnerUserId).IsRequired();
            entity.Property(character => character.ChumXml).IsRequired();
            entity.Property(character => character.Revision).IsConcurrencyToken();
            entity.HasIndex(character => new { character.OwnerUserId, character.UpdatedAtUnixMilliseconds });
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(character => character.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
