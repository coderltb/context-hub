namespace ContextHub.Infrastructure.Constants.Telemetry;

/// <summary>
/// Contains service-owned parameterized atomic statements; consumers never supply SQL.
/// </summary>
internal static class TelemetrySqlConstants
{
    /// <summary>
    /// Merges only open client signals while retaining the Owner's workflow decision.
    /// </summary>
    internal const string UPSERT_CLIENT_SIGNAL = """
        INSERT INTO recommendation."Recommendation"
            ("Id","PackageId","Type","Origin","DedupeKey","Title","Description","Context","Status", "SignalCount",
             "LastSeenAt","CreatedAt","CreatedBy","UpdatedAt","UpdatedBy")
        SELECT @Id,@PackageId,@Type,'client_signal',@DedupeKey,@Title,@Description,CAST(@Context AS jsonb),'new',1,
               @Now,@Now,NULL,@Now,NULL
        WHERE @PackageId IS NULL OR EXISTS (
            SELECT 1 FROM catalog."Package" p JOIN catalog."Version" v ON v."PackageId" = p."Id"
            WHERE p."Id"=@PackageId AND p."IsActive" AND v."Number"=@Version AND v."Status" IN ('published','deprecated'))
        ON CONFLICT ("DedupeKey") WHERE "Origin"='client_signal' AND "Status" IN ('new','accepted')
        DO UPDATE SET "SignalCount" = recommendation."Recommendation"."SignalCount" + 1,
                      "LastSeenAt" = GREATEST(recommendation."Recommendation"."LastSeenAt",EXCLUDED."LastSeenAt"),
                      "UpdatedAt" = GREATEST(recommendation."Recommendation"."UpdatedAt",EXCLUDED."UpdatedAt")
        RETURNING (xmax = 0);
        """;
}
