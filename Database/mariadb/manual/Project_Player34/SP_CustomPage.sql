-- Hand-written: the T-SQL original builds paging queries with dynamic SQL (TOP / NOT IN tricks).
-- Returns one page of <FdShow> FROM <QueryStr> WHERE <QueryWhere> ORDER BY <FdOrder> (else <FdKey>), and the row count.
DROP PROCEDURE IF EXISTS `SP_CustomPage`$$
CREATE PROCEDURE `SP_CustomPage`(IN p_QueryStr VARCHAR(4000), IN p_QueryWhere VARCHAR(4000), IN p_PageSize INT, IN p_PageCurrent INT,
  IN p_FdShow VARCHAR(4000), IN p_FdOrder VARCHAR(1000), IN p_FdKey VARCHAR(250), INOUT p_TotalRow INT, OUT p_ReturnValue INT)
proc: BEGIN
  DECLARE v_pages INT DEFAULT 1;
  DECLARE v_where VARCHAR(4000);
  DECLARE v_order VARCHAR(1000);
  SET p_ReturnValue = 0;
  -- The original's guard against statements smuggled into the parameters.
  IF `FiltSql`(p_QueryStr) = 0 OR `FiltSql`(IFNULL(p_QueryWhere, '')) = 0 OR `FiltSql`(IFNULL(p_FdShow, '')) = 0 OR `FiltSql`(IFNULL(p_FdKey, '')) = 0 THEN
    LEAVE proc;
  END IF;
  SET p_PageSize = IF(IFNULL(p_PageSize, 0) < 1, 10, p_PageSize);
  SET p_FdShow = IF(IFNULL(p_FdShow, '') = '', '*', p_FdShow);
  SET v_where = IF(IFNULL(p_QueryWhere, '') = '', '1=1', p_QueryWhere);
  SET v_order = IF(IFNULL(p_FdOrder, '') <> '', p_FdOrder, IFNULL(p_FdKey, ''));
  -- Callers pass T-SQL's NEWID() to shuffle the rows.
  SET v_order = REGEXP_REPLACE(v_order, '(?i)newid\\(\\)', 'RAND()');
  SET @cp_sql = CONCAT('SELECT COUNT(*) INTO @cp_total FROM ', p_QueryStr, ' WHERE ', v_where);
  PREPARE cp_stmt FROM @cp_sql;
  EXECUTE cp_stmt;
  DEALLOCATE PREPARE cp_stmt;
  SET p_TotalRow = @cp_total;
  IF p_TotalRow > 0 THEN
    SET v_pages = (p_TotalRow + p_PageSize - 1) DIV p_PageSize;
  END IF;
  SET p_PageCurrent = LEAST(GREATEST(IFNULL(p_PageCurrent, 1), 1), v_pages);
  SET @cp_sql = CONCAT('SELECT ', p_FdShow, ' FROM ', p_QueryStr, ' WHERE ', v_where,
    IF(v_order <> '', CONCAT(' ORDER BY ', v_order), ''), ' LIMIT ', (p_PageCurrent - 1) * p_PageSize, ', ', p_PageSize);
  PREPARE cp_stmt FROM @cp_sql;
  EXECUTE cp_stmt;
  DEALLOCATE PREPARE cp_stmt;
END$$
