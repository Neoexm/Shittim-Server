using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShittimServer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMailParcelColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "IsReceived",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "ParcelInfos",
                table: "Mails");

            migrationBuilder.RenameColumn(
                name: "RemainParcelInfos",
                table: "Mails",
                newName: "ReceiptDate");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpireDate",
                table: "Mails",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "ParcelInfosJson",
                table: "Mails",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemainParcelInfosJson",
                table: "Mails",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParcelInfosJson",
                table: "Mails");

            migrationBuilder.DropColumn(
                name: "RemainParcelInfosJson",
                table: "Mails");

            migrationBuilder.RenameColumn(
                name: "ReceiptDate",
                table: "Mails",
                newName: "RemainParcelInfos");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpireDate",
                table: "Mails",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "Mails",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReceived",
                table: "Mails",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ParcelInfos",
                table: "Mails",
                type: "TEXT",
                nullable: true);
        }
    }
}
