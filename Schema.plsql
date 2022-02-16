--
-- IDistributedCache implementation for Oracle Database
--
begin
    for x in (
    select count(*) as rec_count from USER_TABLES where table_name = 'SESSION_CACHE' 
    )
    loop
        if x.rec_count = 0 then
            execute immediate '
		        CREATE TABLE SESSION_CACHE 
		        (
			        Id NVARCHAR2(1024) NOT NULL ENABLE,
                    Value BLOB NOT NULL ENABLE,
                    ExpiresAtTime TIMESTAMP NOT NULL,
                    SlidingExpirationInSeconds NUMBER,
			        AbsoluteExpiration TIMESTAMP
		        )
            ';
            execute immediate '
              ALTER TABLE SESSION_CACHE ADD CONSTRAINT PK_SESSION_CACHE PRIMARY KEY (
                Id
              )
            ';
            execute immediate '
		        CREATE INDEX IDX_SESSION_CACHE_EAT ON SESSION_CACHE (ExpiresAtTime) 
            ';

        end if;
    end loop;
    EXCEPTION WHEN OTHERS THEN NULL;
end;
/
COMMENT ON TABLE SESSION_CACHE IS 'Distributed Cache Storage for Web Services.'
/
COMMENT ON COLUMN SESSION_CACHE.Id IS 'Uniquely identifier to store a key value.'
/
COMMENT ON COLUMN SESSION_CACHE.Value IS 'Stores data to be cached.'
/
COMMENT ON COLUMN SESSION_CACHE.ExpiresAtTime IS 'Idle expiration time.'
/
COMMENT ON COLUMN SESSION_CACHE.SlidingExpirationInSeconds IS 'Record will be in expired state after last access time plus this.'
/
COMMENT ON COLUMN SESSION_CACHE.AbsoluteExpiration IS 'Max lifetime of the record.'
/


create or replace PACKAGE SESSION_CACHE_PKG AS 
    PROCEDURE Put_Cache(p_key IN VARCHAR2, p_value IN BLOB, p_slidingExpirationInSeconds LONG DEFAULT NULL, p_absoluteExpiration TIMESTAMP DEFAULT NULL);
    PROCEDURE Get_Cache(p_key IN VARCHAR2, p_value OUT BLOB);
    PROCEDURE Delete_Cache(p_key IN VARCHAR2);
    PROCEDURE DeleteExpiredCache;
END;
/
create or replace PACKAGE body SESSION_CACHE_PKG AS
    FUNCTION Check_Cache(p_key IN VARCHAR2) 
        RETURN INTEGER
    AS
        v_result INTEGER;
    BEGIN
        SELECT COUNT(*) INTO v_result FROM SESSION_CACHE WHERE Id = p_key;
        RETURN v_result;
    END;
  
    PROCEDURE Put_Cache(p_key IN VARCHAR2, p_value IN BLOB, p_slidingExpirationInSeconds LONG DEFAULT NULL, p_absoluteExpiration TIMESTAMP DEFAULT NULL)
    AS
    BEGIN
        IF Check_Cache(p_key) = 0 THEN
            INSERT INTO SESSION_CACHE (Id, Value, ExpiresAtTime, SlidingExpirationInSeconds, AbsoluteExpiration)
                VALUES (p_key, p_value, 
                    case
                        when (p_absoluteExpiration IS NULL) OR (systimestamp + (p_slidingExpirationInSeconds/(24*60*60)) < p_absoluteExpiration)
                            then systimestamp + (p_slidingExpirationInSeconds/(24*60*60))
                        else p_absoluteExpiration end,
                    p_slidingExpirationInSeconds, p_absoluteExpiration);
        ELSE
            UPDATE SESSION_CACHE
                SET Value = value, ExpiresAtTime = 
                    case 
                        when (p_absoluteExpiration IS NULL) OR (systimestamp + (p_slidingExpirationInSeconds/(24*60*60)) < p_absoluteExpiration)
                             then systimestamp + (p_slidingExpirationInSeconds/(24*60*60))
                        else AbsoluteExpiration end,
                SlidingExpirationInSeconds = p_slidingExpirationInSeconds, AbsoluteExpiration = p_absoluteExpiration
            WHERE Id = p_key;
        END IF;
    END;

    PROCEDURE Get_Cache(p_key IN VARCHAR2, p_value OUT BLOB)
    AS
    BEGIN
        IF Check_Cache(p_key) > 0 THEN
            UPDATE SESSION_CACHE
            SET ExpiresAtTime =
                case 
                    when (AbsoluteExpiration IS NULL) OR (systimestamp + (SlidingExpirationInSeconds/(24*60*60)) < AbsoluteExpiration)
                         then systimestamp + (SlidingExpirationInSeconds/(24*60*60))
                    else AbsoluteExpiration end
            WHERE Id = p_key
            AND systimestamp <= ExpiresAtTime
            AND SlidingExpirationInSeconds IS NOT NULL 
            AND (AbsoluteExpiration IS NULL OR AbsoluteExpiration <> ExpiresAtTime);
        
            SELECT value INTO p_value FROM SESSION_CACHE WHERE Id = p_key;
        ELSE
            p_value := NULL;
        END IF;
    END;
 
    PROCEDURE Delete_Cache(p_key IN VARCHAR2)
    AS
    BEGIN
        DELETE FROM SESSION_CACHE WHERE Id = p_key;
    END;
  
    PROCEDURE DeleteExpiredCache
    AS
    BEGIN
        DELETE FROM SESSION_CACHE WHERE ExpiresAtTime < systimestamp;
    END;

END;
/
