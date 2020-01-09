using Microsoft.AspNetCore.Identity;
using NHibernate.Mapping.ByCode;
using NHibernate.Mapping.ByCode.Conformist;

namespace NHibernate.AspNetCore.Identity {

    public class IdentityUserClaim : IdentityUserClaim<string> { }

    public class IdentityUserClaimMappingPostgreSql : ClassMapping<IdentityUserClaim> {

        public IdentityUserClaimMappingPostgreSql() {
            Schema("public");
            Table("aspnet_user_claims");
            Id(e => e.Id, id => {
                id.Column("id");
                id.Type(NHibernateUtil.Int32);
                id.Generator(Generators.Sequence, g => {
                    g.Params(new { sequence = "snow_flake_id_seq" });
                });
            });
            Property(e => e.ClaimType, prop => {
                prop.Column("claim_type");
                prop.Type(NHibernateUtil.String);
                prop.Length(1024);
                prop.NotNullable(true);
            });
            Property(e => e.ClaimValue, prop => {
                prop.Column("claim_value");
                prop.Type(NHibernateUtil.String);
                prop.Length(1024);
                prop.NotNullable(true);
            });
            Property(e => e.UserId, prop => {
                prop.Column("user_id");
                prop.Type(NHibernateUtil.String);
                prop.Length(32);
                prop.NotNullable(true);
            });
        }

    }

}
