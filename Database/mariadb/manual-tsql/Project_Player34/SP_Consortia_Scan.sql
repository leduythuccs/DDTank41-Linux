-- Rewritten for the MariaDB translator. The original collected the IDs of guilds to disband in a string and ran
-- "... WHERE ConsortiaID IN (" + @ids + ")" through EXEC; this keeps them in a temporary table instead. MariaDB cannot
-- read one temporary table twice in a statement, so "Consortia1ID IN (...) OR Consortia2ID IN (...)" is two UPDATEs.
-- Same result: the same rows change, in one transaction, and @NoticeID returns "id1,id2" as before.
CREATE PROCEDURE [dbo].[SP_Consortia_Scan]
 @NoticeID nvarchar(4000) output
AS
set @NoticeID=''
begin tran
create table #Disband (ConsortiaID int)
insert into #Disband select ConsortiaID from Consortia where datediff(dd, WarnDate,getdate())>14 and IsExist=1
update Consortia set IsExist=0 where ConsortiaID in (select ConsortiaID from #Disband)
select @NoticeID = @NoticeID + cast(ConsortiaID as varchar(20)) + ',' from #Disband
if len(@NoticeID)>0
begin
 set @NoticeID = substring(@NoticeID,1,len(@NoticeID)-1)
 Update Consortia_Equip_Control set IsExist = 0 where ConsortiaID in (select ConsortiaID from #Disband) and IsExist=1
 Update Consortia_Apply_Users set IsExist = 0 where ConsortiaID in (select ConsortiaID from #Disband) and IsExist=1
 Update Consortia_Invite_Users set IsExist = 0 where ConsortiaID in (select ConsortiaID from #Disband) and IsExist=1
 Update Consortia_Apply_Ally set IsExist = 0 where Consortia1ID in (select ConsortiaID from #Disband) and IsExist=1
 Update Consortia_Apply_Ally set IsExist = 0 where Consortia2ID in (select ConsortiaID from #Disband) and IsExist=1
 Update Consortia_Duty set IsExist = 0 where ConsortiaID in (select ConsortiaID from #Disband) and IsExist=1
 Update Consortia_Users set IsExist = 0 where ConsortiaID in (select ConsortiaID from #Disband) and IsExist=1
 Update Consortia_Ally set IsExist = 0 where Consortia1ID in (select ConsortiaID from #Disband) and IsExist=1
 Update Consortia_Ally set IsExist = 0 where Consortia2ID in (select ConsortiaID from #Disband) and IsExist=1
 Update Consortia_Event set IsExist = 0 where ConsortiaID in (select ConsortiaID from #Disband) and IsExist=1
 Update Sys_Users_Detail set ConsortiaID = 0,RichesOffer=0,RichesRob=0 where ConsortiaID in (select ConsortiaID from #Disband) and IsExist=1
end
drop table #Disband
update Consortia set DeductDate=getdate(),WarnDate=(case when a.Riches>0 then getdate() else a.WarnDate end), Riches=Riches - (select (case when a.Riches>b.Deduct then  Deduct else a.Riches end) from Consortia_Level b where b.[Level]=a.[Level])
  from Consortia a where datediff(dd, DeductDate,getdate())>7 and IsExist=1
commit tran
select @NoticeID
return 0
