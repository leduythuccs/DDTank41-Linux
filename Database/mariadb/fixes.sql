-- Fixes to the original game data, applied by deploy/restore-databases.sh after <Db>.data.sql (which is generated
-- from the backups and not edited by hand). Each fix must be safe to run twice.

-- Ball 11422 (pet skill "Hơi Thở Băng Giá" 1297/1305/1543/1553) has no BombPartical: the client then waits for
-- bullet0.swf, which does not exist, and the battle stays at 91% loading. Use the explosion of the same pet's other
-- ice ball (11423). Tank.Request/BallList.xml carries the same fix.
UPDATE `Project_Game34`.`Ball` SET `BombPartical` = '11423' WHERE `ID` = 11422 AND IFNULL(`BombPartical`, '') = '';

-- Pet auto-attack skills (the pet attacks after its owner's shot): Damage is the % of the shot's damage the pet
-- deals, and the server only attacks with a pet when an equipped skill has Damage > 0. 34 of these skills had 0 (so
-- their pet never attacked) and 16 had values tuned for a division by 300 that the server no longer does. Each is
-- set to the % in its own description ("gây 50% sát thương"). Previous value in the comment.
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 10 WHERE `ID` = 7; -- Bong Bóng Gà Lv1 (was 15)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 20 WHERE `ID` = 8; -- Bong Bóng Gà Lv2 (was 25)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 30 WHERE `ID` = 9; -- Bong Bóng Gà Lv3 (was 36)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 10 WHERE `ID` = 100; -- Liệt diệm Lv1 (was 30)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 20 WHERE `ID` = 101; -- Liệt diệm Lv2 (was 40)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 30 WHERE `ID` = 102; -- Liệt diệm Lv3 (was 50)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 10 WHERE `ID` = 121; -- Thiên Thạch Băng Lv1 (was 30)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 20 WHERE `ID` = 122; -- Thiên Thạch Băng Lv2 (was 40)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 30 WHERE `ID` = 123; -- Thiên Thạch Băng Lv3 (was 50)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 30 WHERE `ID` = 260; -- Cưỡi Mây Lv1 (was 200)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 40 WHERE `ID` = 261; -- Cưỡi Mây Lv2 (was 250)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 50 WHERE `ID` = 262; -- Cưỡi Mây Lv3 (was 300)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 65 WHERE `ID` = 280; -- Thức-Lửa Cháy Bập Bùng (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 65 WHERE `ID` = 288; -- Thức-Hươu Xuất Kích (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 30 WHERE `ID` = 292; -- Xanh Biếc-Đánh Lv1 (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 40 WHERE `ID` = 293; -- Xanh Biếc-Đánh Lv2 (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 65 WHERE `ID` = 301; -- Thức-Cuồng Vũ (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 65 WHERE `ID` = 308; -- Thức-Cưỡi Mây (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 65 WHERE `ID` = 318; -- Thức-Yoyo Xuất Kích (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 36 WHERE `ID` = 464; -- Dấu Vết Móng Vuốt Lv3 (was 50)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 65 WHERE `ID` = 497; -- Thức-Dấu Vết Móng Vuốt (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 30 WHERE `ID` = 825; -- Cắn Xé Dữ Dội (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 40 WHERE `ID` = 826; -- Cắn Xé Dữ Dội (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 50 WHERE `ID` = 827; -- Cắn Xé Dữ Dội (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 15 WHERE `ID` = 880; -- Heo Ủi Lv.1 (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 25 WHERE `ID` = 881; -- Heo Ủi Lv.2 (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 36 WHERE `ID` = 882; -- Heo Ủi Lv.3 (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 65 WHERE `ID` = 993; -- Thức-Đá Sương Đông (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 30 WHERE `ID` = 1157; -- Phượng Hoàng Vẫy Cánh (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 40 WHERE `ID` = 1158; -- Phượng Hoàng Vẫy Cánh (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 50 WHERE `ID` = 1159; -- Phượng Hoàng Vẫy Cánh (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 15 WHERE `ID` = 1221; -- Vuốt Nhọn (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 25 WHERE `ID` = 1222; -- Vuốt Nhọn (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 36 WHERE `ID` = 1223; -- Vuốt Nhọn (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 15 WHERE `ID` = 1260; -- Thỏ Xuất Kích (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 25 WHERE `ID` = 1261; -- Thỏ Xuất Kích (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 36 WHERE `ID` = 1262; -- Thỏ Xuất Kích (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 30 WHERE `ID` = 1291; -- Gió Lốc Băng Ngục (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 40 WHERE `ID` = 1292; -- Gió Lốc Băng Ngục (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 50 WHERE `ID` = 1293; -- Gió Lốc Băng Ngục (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 80 WHERE `ID` = 1496; -- Thức 1-Gió Lốc Băng Ngục (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 150 WHERE `ID` = 1541; -- Thức Tỉnh 2-Gió Lốc Băng Ngục (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 150 WHERE `ID` = 1551; -- Thức Tỉnh 3-Gió Lốc Băng Ngục (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 150 WHERE `ID` = 1560; -- Thức Tỉnh 3-Gió Lốc Băng Ngục (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 100 WHERE `ID` = 1700; -- Thức Tỉnh 2-Dấu Vết Móng Vuốt (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 100 WHERE `ID` = 1707; -- Thức Tỉnh 3-Dấu Vết Móng Vuốt (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 100 WHERE `ID` = 1714; -- Thức Tỉnh 3-Dấu Vết Móng Vuốt (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 100 WHERE `ID` = 1733; -- Thức Tỉnh 2-Cuồng Vũ (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 100 WHERE `ID` = 1742; -- Thức Tỉnh 3-Cuồng Vũ (was 0)
UPDATE `Project_Game34`.`Pet_Skill_Info` SET `Damage` = 100 WHERE `ID` = 1749; -- Thức Tỉnh 3-Cuồng Vũ (was 0)
