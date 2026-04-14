using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShiftHub.Data.Entities;

namespace ShiftHub.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Group> Groups => Set<Group>();
        public DbSet<LogLogin> LogLogins => Set<LogLogin>();
        public DbSet<Mail> Mails => Set<Mail>();
        public DbSet<AppMessage> AppMessages => Set<AppMessage>();
        public DbSet<Staff> Staff => Set<Staff>();
        public DbSet<SubGroup> SubGroups => Set<SubGroup>();
        public DbSet<SubGroupAlert> SubGroupAlerts => Set<SubGroupAlert>();
        public DbSet<SubGroupDay> SubGroupDays => Set<SubGroupDay>();
        public DbSet<SubGroupForm> SubGroupForms => Set<SubGroupForm>();
        public DbSet<SubGroupKey> SubGroupKeys => Set<SubGroupKey>();
        public DbSet<SubGroupMessage> SubGroupMessages => Set<SubGroupMessage>();
        public DbSet<SubGroupNeed> SubGroupNeeds => Set<SubGroupNeed>();
        public DbSet<SubGroupPostGroup> SubGroupPostGroups => Set<SubGroupPostGroup>();
        public DbSet<SubGroupPostMember> SubGroupPostMembers => Set<SubGroupPostMember>();
        public DbSet<SubGroupRegistration> SubGroupRegistrations => Set<SubGroupRegistration>();
        public DbSet<SubGroupShift> SubGroupShifts => Set<SubGroupShift>();
        public DbSet<SubGroupStaff> SubGroupStaff => Set<SubGroupStaff>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<SubGroupKey>().HasNoKey().ToTable("SubGroupKeys");
            builder.Entity<SubGroupKey>().Property(x => x.Id)
                .ValueGeneratedOnAdd();
            builder.Entity<SubGroupKey>().HasKey(x => x.Id);
        }
    }
}
