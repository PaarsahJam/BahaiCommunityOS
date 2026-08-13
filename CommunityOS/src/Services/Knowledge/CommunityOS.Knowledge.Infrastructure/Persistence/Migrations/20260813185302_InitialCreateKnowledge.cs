using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommunityOS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateKnowledge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "knowledge");

            migrationBuilder.CreateTable(
                name: "ai_suggestions",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    model_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    prompt_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    requested_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    review_state = table.Column<int>(type: "integer", nullable: false),
                    reviewed_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_suggestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "answers",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<int>(type: "integer", nullable: false),
                    model_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    suggestion_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accepted = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_answers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "discussions",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discussions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "editions",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    translator = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    publisher = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    edition_year = table.Column<int>(type: "integer", nullable: true),
                    verified = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_editions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organization_unit_references",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_seen_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_unit_references", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "passages",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    edition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_passages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    accepted_answer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    merged_onto_question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "references",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_type = table.Column<int>(type: "integer", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    passage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    edition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_references", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "topics",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "works",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    original_language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    default_language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    work_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_works", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "answer_revisions",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    revised_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    answer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_answer_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_answer_revisions_answers_answer_id",
                        column: x => x.answer_id,
                        principalSchema: "knowledge",
                        principalTable: "answers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "comments",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    discussion_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_comments_discussions_discussion_id",
                        column: x => x.discussion_id,
                        principalSchema: "knowledge",
                        principalTable: "discussions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "passage_revisions",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    corrected_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    passage_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_passage_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_passage_revisions_passages_passage_id",
                        column: x => x.passage_id,
                        principalSchema: "knowledge",
                        principalTable: "passages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "moderation_flags",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    flagged_by = table.Column<Guid>(type: "uuid", nullable: true),
                    flagged_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moderation_flags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_moderation_flags_questions_question_id",
                        column: x => x.question_id,
                        principalSchema: "knowledge",
                        principalTable: "questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_lifecycle_events",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    to_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_lifecycle_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_lifecycle_events_questions_question_id",
                        column: x => x.question_id,
                        principalSchema: "knowledge",
                        principalTable: "questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tags_questions_question_id",
                        column: x => x.question_id,
                        principalSchema: "knowledge",
                        principalTable: "questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_suggestions_question_id",
                schema: "knowledge",
                table: "ai_suggestions",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_answer_revisions_answer_id",
                schema: "knowledge",
                table: "answer_revisions",
                column: "answer_id");

            migrationBuilder.CreateIndex(
                name: "IX_answers_question_id",
                schema: "knowledge",
                table: "answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_comments_discussion_id",
                schema: "knowledge",
                table: "comments",
                column: "discussion_id");

            migrationBuilder.CreateIndex(
                name: "IX_discussions_question_id",
                schema: "knowledge",
                table: "discussions",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_editions_work_id",
                schema: "knowledge",
                table: "editions",
                column: "work_id");

            migrationBuilder.CreateIndex(
                name: "IX_moderation_flags_question_id",
                schema: "knowledge",
                table: "moderation_flags",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_organization_unit_references_organization_id",
                schema: "knowledge",
                table: "organization_unit_references",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_passage_revisions_passage_id",
                schema: "knowledge",
                table: "passage_revisions",
                column: "passage_id");

            migrationBuilder.CreateIndex(
                name: "IX_passages_edition_id",
                schema: "knowledge",
                table: "passages",
                column: "edition_id");

            migrationBuilder.CreateIndex(
                name: "IX_question_lifecycle_events_question_id",
                schema: "knowledge",
                table: "question_lifecycle_events",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_questions_organization_unit_id",
                schema: "knowledge",
                table: "questions",
                column: "organization_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_references_owner_type_owner_id",
                schema: "knowledge",
                table: "references",
                columns: new[] { "owner_type", "owner_id" });

            migrationBuilder.CreateIndex(
                name: "IX_references_passage_id",
                schema: "knowledge",
                table: "references",
                column: "passage_id");

            migrationBuilder.CreateIndex(
                name: "IX_tags_question_id",
                schema: "knowledge",
                table: "tags",
                column: "question_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_suggestions",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "answer_revisions",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "comments",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "editions",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "moderation_flags",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "organization_unit_references",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "passage_revisions",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "question_lifecycle_events",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "references",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "tags",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "topics",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "works",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "answers",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "discussions",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "passages",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "questions",
                schema: "knowledge");
        }
    }
}
