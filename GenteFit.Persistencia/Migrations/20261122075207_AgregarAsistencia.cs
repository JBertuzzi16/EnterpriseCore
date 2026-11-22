using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenteFit.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAsistencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Asistio",
                table: "Reservas",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Asistio",
                table: "Reservas");
        }
    }
}
