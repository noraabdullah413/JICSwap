// Source: database/seeders/DatabaseSeeder.php
namespace JicRss.Data;

/// <summary>
/// Seed the application's database.
/// </summary>
public static class DbSeeder
{
    /// <summary>run(): called from Program.cs on startup (php artisan db:seed).</summary>
    public static void Run(AppDbContext db)
    {
        // Each project defines its own actor seeders here
        // (e.g. $this->call([AdminSeeder::class, CustomerSeeder::class, ...]))
        // or loads a raw SQL insert script instead — see implementation/database/.
        //
        // The Laravel seeder is intentionally empty: the JIC-RSS seed data is the plain INSERT script
        // implementation/database/02_insert_data.sql, run by hand after the schema script.
        // TODO(Laravel): DatabaseSeeder::run() is an empty stub; nothing to translate. Seed data stays in
        // implementation/database/02_insert_data.sql (run it after jicrss.sql).
    }
}
