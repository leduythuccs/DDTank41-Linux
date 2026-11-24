-- Fixes to the original game data, applied by deploy/restore-databases.sh after <Db>.data.sql (which is generated
-- from the backups and not edited by hand). Each fix must be safe to run twice.

-- Ball 11422 (pet skill "Hơi Thở Băng Giá" 1297/1305/1543/1553) has no BombPartical: the client then waits for
-- bullet0.swf, which does not exist, and the battle stays at 91% loading. Use the explosion of the same pet's other
-- ice ball (11423). Tank.Request/BallList.xml carries the same fix.
UPDATE `Project_Game34`.`Ball` SET `BombPartical` = '11423' WHERE `ID` = 11422 AND IFNULL(`BombPartical`, '') = '';
