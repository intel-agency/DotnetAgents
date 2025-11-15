using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotnetAgents.AgentApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskTrackingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "AgentTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AgentTasks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CurrentIteration",
                table: "AgentTasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "AgentTasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedAt",
                table: "AgentTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxIterations",
                table: "AgentTasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Result",
                table: "AgentTasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "AgentTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdateCount",
                table: "AgentTasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "AgentTasks");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AgentTasks");

            migrationBuilder.DropColumn(
                name: "CurrentIteration",
                table: "AgentTasks");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "AgentTasks");

            migrationBuilder.DropColumn(
                name: "LastUpdatedAt",
                table: "AgentTasks");

            migrationBuilder.DropColumn(
                name: "MaxIterations",
                table: "AgentTasks");

            migrationBuilder.DropColumn(
                name: "Result",
                table: "AgentTasks");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "AgentTasks");

            migrationBuilder.DropColumn(
                name: "UpdateCount",
                table: "AgentTasks");
        }
    }
}
