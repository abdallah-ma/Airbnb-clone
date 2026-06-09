using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirbnbMVP.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddListingReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ListingId",
                table: "reviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_reviews_ListingId",
                table: "reviews",
                column: "ListingId");

            migrationBuilder.AddForeignKey(
                name: "FK_reviews_listings_ListingId",
                table: "reviews",
                column: "ListingId",
                principalTable: "listings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_reviews_listings_ListingId",
                table: "reviews");

            migrationBuilder.DropIndex(
                name: "IX_reviews_ListingId",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "ListingId",
                table: "reviews");
        }
    }
}
