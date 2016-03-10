CREATE TABLE "public"."Errors" (
"Id" serial8 NOT NULL,
"GUID" uuid NOT NULL,
"ApplicationName" varchar(50) NOT NULL,
"MachineName" varchar(50) NOT NULL,
"CreationDate" timestamp NOT NULL,
"Type" varchar(100) NOT NULL,
"IsProtected" bool DEFAULT False NOT NULL,
"Host" varchar(100),
"Url" varchar(500),
"HTTPMethod" varchar(10),
"IPAddress" varchar(40),
"Source" varchar(100),
"Message" varchar(1000),
"Detail" text,
"StatusCode" int4,
"SQL" text,
"DeletionDate" timestamp,
"FullJson" text,
"ErrorHash" int4,
"DuplicateCount" int4 DEFAULT 1 NOT NULL,
PRIMARY KEY ("Id")
)
WITH (OIDS=FALSE)
;

CREATE UNIQUE INDEX "IX_Exceptions_GUID_ApplicationName_DeletionDate_CreationDate" ON "public"."Errors" ("GUID", "ApplicationName", "CreationDate" DESC, "DeletionDate");

CREATE INDEX "IX_Exceptions_ErrorHash_ApplicationName_CreationDate_DeletionDa" ON "public"."Errors" ("ApplicationName", "CreationDate" DESC, "DeletionDate", "ErrorHash");

CREATE INDEX "IX_Exceptions_ApplicationName_DeletionDate_CreationDate_Filtere" ON "public"."Errors" ("ApplicationName", "CreationDate" DESC, "DeletionDate") WHERE "DeletionDate" IS NULL;

