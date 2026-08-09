-- Creates the per-service databases used by the CommunityOS platform.
-- Executed automatically by the postgres:16-alpine entrypoint on first boot
-- (files in /docker-entrypoint-initdb.d are run in alphabetical order).

CREATE DATABASE communityos_identity;
CREATE DATABASE communityos_events;
CREATE DATABASE communityos_content;
CREATE DATABASE communityos_community;
CREATE DATABASE communityos_enrollment;
CREATE DATABASE communityos_reporting;
