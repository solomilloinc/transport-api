using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace transport.infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameTripDirectionToTripPickupStop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "TripDirection",
                newName: "TripPickupStop");

            migrationBuilder.RenameColumn(
                name: "TripDirectionId",
                table: "TripPickupStop",
                newName: "TripPickupStopId");

            migrationBuilder.RenameIndex(
                name: "IX_TripDirection_DirectionId",
                table: "TripPickupStop",
                newName: "IX_TripPickupStop_DirectionId");

            migrationBuilder.RenameIndex(
                name: "IX_TripDirection_TripId_DirectionId",
                table: "TripPickupStop",
                newName: "IX_TripPickupStop_TripId_DirectionId");

            // Drop old FKs and recreate with new names
            migrationBuilder.DropForeignKey(
                name: "FK_TripDirection_Direction_DirectionId",
                table: "TripPickupStop");

            migrationBuilder.DropForeignKey(
                name: "FK_TripDirection_Trip_TripId",
                table: "TripPickupStop");

            migrationBuilder.AddForeignKey(
                name: "FK_TripPickupStop_Direction_DirectionId",
                table: "TripPickupStop",
                column: "DirectionId",
                principalTable: "Direction",
                principalColumn: "DirectionId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TripPickupStop_Trip_TripId",
                table: "TripPickupStop",
                column: "TripId",
                principalTable: "Trip",
                principalColumn: "TripId",
                onDelete: ReferentialAction.Cascade);

            // Rename PK constraint
            migrationBuilder.Sql("EXEC sp_rename N'PK_TripDirection', N'PK_TripPickupStop', N'OBJECT'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("EXEC sp_rename N'PK_TripPickupStop', N'PK_TripDirection', N'OBJECT'");

            migrationBuilder.DropForeignKey(
                name: "FK_TripPickupStop_Direction_DirectionId",
                table: "TripPickupStop");

            migrationBuilder.DropForeignKey(
                name: "FK_TripPickupStop_Trip_TripId",
                table: "TripPickupStop");

            migrationBuilder.AddForeignKey(
                name: "FK_TripDirection_Direction_DirectionId",
                table: "TripPickupStop",
                column: "DirectionId",
                principalTable: "Direction",
                principalColumn: "DirectionId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TripDirection_Trip_TripId",
                table: "TripPickupStop",
                column: "TripId",
                principalTable: "Trip",
                principalColumn: "TripId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.RenameIndex(
                name: "IX_TripPickupStop_DirectionId",
                table: "TripPickupStop",
                newName: "IX_TripDirection_DirectionId");

            migrationBuilder.RenameIndex(
                name: "IX_TripPickupStop_TripId_DirectionId",
                table: "TripPickupStop",
                newName: "IX_TripDirection_TripId_DirectionId");

            migrationBuilder.RenameColumn(
                name: "TripPickupStopId",
                table: "TripPickupStop",
                newName: "TripDirectionId");

            migrationBuilder.RenameTable(
                name: "TripPickupStop",
                newName: "TripDirection");
        }
    }
}
