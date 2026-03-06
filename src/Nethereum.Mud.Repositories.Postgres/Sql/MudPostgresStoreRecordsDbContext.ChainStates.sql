CREATE TABLE IF NOT EXISTS chainstates (
    rowindex BIGSERIAL PRIMARY KEY,
    lastcanonicalblocknumber TEXT,
    lastcanonicalblockhash TEXT,
    finalizedblocknumber TEXT,
    chainid INTEGER,
    creationdate TIMESTAMP,
    lastupdatedate TIMESTAMP
);

CREATE INDEX IF NOT EXISTS ix_chainstates_lastcanonicalblocknumber
    ON chainstates (lastcanonicalblocknumber);
