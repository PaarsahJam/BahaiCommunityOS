-- Creates the per-service databases used by the CommunityOS platform.
-- Executed automatically by the postgres:16-alpine entrypoint on first boot
-- (files in /docker-entrypoint-initdb.d are run in alphabetical order).

CREATE DATABASE communityos_identity;
CREATE DATABASE communityos_events;
CREATE DATABASE communityos_content;
CREATE DATABASE communityos_community;
CREATE DATABASE communityos_enrollment;
CREATE DATABASE communityos_reporting;
CREATE DATABASE communityos_authorization;
CREATE DATABASE communityos_organization;
CREATE DATABASE communityos_knowledge;
CREATE DATABASE communityos_documents;
CREATE DATABASE communityos_records;
CREATE DATABASE communityos_workflow;
CREATE DATABASE communityos_notifications;
CREATE DATABASE communityos_search;
CREATE DATABASE communityos_audit;
CREATE DATABASE communityos_correspondence;
CREATE DATABASE communityos_localization;
