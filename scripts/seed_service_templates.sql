-- SQL Script to Seed Sample Service Templates and Checklist Items
-- Target Database: MySQL (Service Management DB)

-- 1. Insert Service Templates
-- We assume ShopId = 1 for the default shop. Replace with the correct shop id if necessary.
INSERT INTO `service_templates` (`ShopId`, `Name`, `Description`, `IsActive`, `CreatedAt`)
VALUES 
(1, 'Smartphone Screen Replacement', 'Standard checklist for replacing mobile device screen panels.', 1, NOW()),
(1, 'Laptop Dust Cleaning & Thermal Repaste', 'Thermal maintenance and internal dust cleaning process.', 1, NOW()),
(1, 'OS Reinstallation & Optimization', 'Standard clean operating system installation check.', 1, NOW());

-- 2. Insert Checklist Items for 'Smartphone Screen Replacement'
SET @template_id_screen = (SELECT `Id` FROM `service_templates` WHERE `ShopId` = 1 AND `Name` = 'Smartphone Screen Replacement' LIMIT 1);

INSERT INTO `service_template_checklist_items` (`ShopId`, `ServiceTemplateId`, `Title`, `Description`, `SortOrder`)
VALUES
(1, @template_id_screen, 'Perform pre-repair diagnostics (touch, display, camera check)', NULL, 1),
(1, @template_id_screen, 'Power off device and remove SIM tray', NULL, 2),
(1, @template_id_screen, 'Heat device and carefully remove back cover/glass', NULL, 3),
(1, @template_id_screen, 'Disconnect battery and old screen panel cables', NULL, 4),
(1, @template_id_screen, 'Clean frame adhesive residue', NULL, 5),
(1, @template_id_screen, 'Test new screen panel functionality before gluing', NULL, 6),
(1, @template_id_screen, 'Apply frame adhesive and assemble screen', NULL, 7),
(1, @template_id_screen, 'Clamp device and perform post-repair diagnostics', NULL, 8);

-- 3. Insert Checklist Items for 'Laptop Dust Cleaning & Thermal Repaste'
SET @template_id_thermal = (SELECT `Id` FROM `service_templates` WHERE `ShopId` = 1 AND `Name` = 'Laptop Dust Cleaning & Thermal Repaste' LIMIT 1);

INSERT INTO `service_template_checklist_items` (`ShopId`, `ServiceTemplateId`, `Title`, `Description`, `SortOrder`)
VALUES
(1, @template_id_thermal, 'Verify initial system temperatures under stress test', NULL, 1),
(1, @template_id_thermal, 'Disassemble chassis and disconnect battery power source', NULL, 2),
(1, @template_id_thermal, 'Clean dust from cooling fans, heatsink fins, and vents', NULL, 3),
(1, @template_id_thermal, 'Remove old thermal paste using isopropyl alcohol', NULL, 4),
(1, @template_id_thermal, 'Apply premium thermal paste to CPU/GPU dies', NULL, 5),
(1, @template_id_thermal, 'Reassemble thermal unit and chassis screws', NULL, 6),
(1, @template_id_thermal, 'Boot system and verify load temperatures are within range', NULL, 7);

-- 4. Insert Checklist Items for 'OS Reinstallation & Optimization'
SET @template_id_os = (SELECT `Id` FROM `service_templates` WHERE `ShopId` = 1 AND `Name` = 'OS Reinstallation & Optimization' LIMIT 1);

INSERT INTO `service_template_checklist_items` (`ShopId`, `ServiceTemplateId`, `Title`, `Description`, `SortOrder`)
VALUES
(1, @template_id_os, 'Perform backup of customer data and licenses', NULL, 1),
(1, @template_id_os, 'Wipe target storage drive and clean install OS', NULL, 2),
(1, @template_id_os, 'Install chipsets, graphics, network, and audio drivers', NULL, 3),
(1, @template_id_os, 'Run system OS updates and security patches', NULL, 4),
(1, @template_id_os, 'Restore backed-up customer files to user directory', NULL, 5),
(1, @template_id_os, 'Verify hardware components function properly', NULL, 6);
