using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using NHibernate.Linq;

namespace NHibernate.AspNetCore.Identity {

    public class UserOnlyStore<TUser> : UserStoreBase<TUser, string, IdentityUserClaim, IdentityUserLogin, IdentityUserToken>,
        IProtectedUserStore<TUser> where TUser : IdentityUser {

        private ISession session;

        public bool AutoFlushChanges { get; set; } = true;

        public override IQueryable<TUser> Users => session.Query<TUser>();

        private IQueryable<IdentityUserClaim> UserClaims => session.Query<IdentityUserClaim>();

        private IQueryable<IdentityUserLogin> UserLogins => session.Query<IdentityUserLogin>();

        private IQueryable<IdentityUserToken> UserTokens => session.Query<IdentityUserToken>();


        public UserOnlyStore(
            ISession session,
            IdentityErrorDescriber describer
        ) : base(describer ?? new IdentityErrorDescriber()) {
            if (session == null) {
                throw new ArgumentNullException(nameof(session));
            }
            this.session = session;
        }

        public override async Task<IdentityResult> CreateAsync(
            TUser user,
            CancellationToken cancellationToken = new CancellationToken()
        ) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null) {
                throw new ArgumentNullException(nameof(user));
            }
            await session.SaveAsync(user, cancellationToken);
            await FlushChanges(cancellationToken);
            return IdentityResult.Success;
        }

        public override async Task<IdentityResult> UpdateAsync(
            TUser user,
            CancellationToken cancellationToken = new CancellationToken()
        ) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null) {
                throw new ArgumentNullException(nameof(user));
            }
            var exists = await Users.AnyAsync(
                u => u.Id.Equals(user.Id),
                cancellationToken
            );
            if (!exists) {
                return IdentityResult.Failed(
                    new IdentityError {
                        Code = "UserNotExist",
                        Description = $"User with id {user.Id} does not exists!"
                    }
                );
            }
            await session.MergeAsync(user, cancellationToken);
            await FlushChanges(cancellationToken);
            return IdentityResult.Success;
        }

        public override async Task<IdentityResult> DeleteAsync(
            TUser user,
            CancellationToken cancellationToken = new CancellationToken()
        ) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null) {
                throw new ArgumentNullException(nameof(user));
            }
            await session.DeleteAsync(user, cancellationToken);
            await FlushChanges(cancellationToken);
            return IdentityResult.Success;
        }

        public override async Task<TUser> FindByIdAsync(
            string userId,
            CancellationToken cancellationToken = new CancellationToken()
        ) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            var id = ConvertIdFromString(userId);
            var user = await session.GetAsync<TUser>(id, cancellationToken);
            return user;
        }

        public override async Task<TUser> FindByNameAsync(
            string normalizedUserName,
            CancellationToken cancellationToken = new CancellationToken()
        ) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            var user = await Users.FirstOrDefaultAsync(
                u => u.NormalizedUserName == normalizedUserName,
                cancellationToken
            );
            return user;
        }

        protected override async Task<TUser> FindUserAsync(
            string userId,
            CancellationToken cancellationToken
        ) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            var user = await Users.FirstOrDefaultAsync(
                u => u.Id.Equals(userId),
                cancellationToken
            );
            return user;
        }

        protected override async Task<IdentityUserLogin> FindUserLoginAsync(
            string userId,
            string loginProvider,
            string providerKey,
            CancellationToken cancellationToken
        ) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            var userLogin = await UserLogins.FirstOrDefaultAsync(
                ul => ul.UserId.Equals(userId) && ul.LoginProvider == loginProvider
                    && ul.ProviderKey == providerKey,
                cancellationToken
            );
            return userLogin;
        }

        protected override async Task<IdentityUserLogin> FindUserLoginAsync(
            string loginProvider,
            string providerKey,
            CancellationToken cancellationToken
        ) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            var userLogin = await UserLogins.FirstOrDefaultAsync(
                ul => ul.LoginProvider == loginProvider
                    && ul.ProviderKey == providerKey,
                cancellationToken
            );
            return userLogin;
        }

        public override async Task<IList<Claim>> GetClaimsAsync(
            TUser user,
            CancellationToken cancellationToken = new CancellationToken()
        ) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null) {
                throw new ArgumentNullException(nameof(user));
            }
            var claims = await UserClaims.Where(
                    uc => uc.UserId.Equals(user.Id)
                )
                .Select(c => c.ToClaim())
                .ToListAsync(cancellationToken);
            return claims;
        }

        public override async Task AddClaimsAsync(
            TUser user,
            IEnumerable<Claim> claims,
            CancellationToken cancellationToken = new CancellationToken()
        ) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null) {
                throw new ArgumentNullException(nameof(user));
            }
            if (claims == null) {
                throw new ArgumentNullException(nameof(claims));
            }
            foreach (var claim in claims) {
                await session.SaveAsync(
                    CreateUserClaim(user, claim),
                    cancellationToken
                );
            }
            await FlushChanges(cancellationToken);
        }

        public override async Task ReplaceClaimAsync(
            TUser user,
            Claim claim,
            Claim newClaim,
            CancellationToken cancellationToken = new CancellationToken()
        ) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null) {
                throw new ArgumentNullException(nameof(user));
            }
            if (claim == null) {
                throw new ArgumentNullException(nameof(claim));
            }
            if (newClaim == null) {
                throw new ArgumentNullException(nameof(newClaim));
            }
            var matchedClaims = await UserClaims.Where(
                    uc => uc.UserId.Equals(user.Id) &&
                        uc.ClaimValue == claim.Value
                        && uc.ClaimType == claim.Type
                )
                .ToListAsync(cancellationToken);
            foreach (var matchedClaim in matchedClaims) {
                matchedClaim.ClaimType = newClaim.Type;
                matchedClaim.ClaimValue = newClaim.Value;
                await session.UpdateAsync(matchedClaim, cancellationToken);
            }
            await FlushChanges(cancellationToken);
        }

        public override async Task RemoveClaimsAsync(
            TUser user,
            IEnumerable<Claim> claims,
            CancellationToken cancellationToken = new CancellationToken()
        ) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null) {
                throw new ArgumentNullException(nameof(user));
            }
            if (claims == null) {
                throw new ArgumentNullException(nameof(claims));
            }
            foreach (var claim in claims) {
                var matchedClaims = await UserClaims.Where(
                        uc => uc.UserId.Equals(user.Id) &&
                            uc.ClaimValue == claim.Value
                            && uc.ClaimType == claim.Type
                    )
                    .ToListAsync(cancellationToken);
                foreach (var matchedClaim in matchedClaims) {
                    await session.DeleteAsync(matchedClaim, cancellationToken);
                }
            }
            await FlushChanges(cancellationToken);
        }

        public override async Task<IList<TUser>> GetUsersForClaimAsync(
            Claim claim,
            CancellationToken cancellationToken = new CancellationToken()
        ) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (claim == null) {
                throw new ArgumentNullException(nameof(claim));
            }
            var query = from userclaims in UserClaims
                join user in Users on userclaims.UserId equals user.Id
                where userclaims.ClaimValue == claim.Value
                    && userclaims.ClaimType == claim.Type
                select user;
            return await query.ToListAsync(cancellationToken);
        }

        protected override async Task<IdentityUserToken> FindTokenAsync(
            TUser user,
            string loginProvider,
            string name,
            CancellationToken cancellationToken
        ) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null) {
                throw new ArgumentNullException(nameof(user));
            }
            var token = await UserTokens.FirstOrDefaultAsync(
                ut => ut.UserId.Equals(user.Id) &&
                    ut.LoginProvider == loginProvider
                    && ut.Name == name,
                cancellationToken);
            return token;
        }

        protected override async Task AddUserTokenAsync(
            IdentityUserToken token
        ) {
            ThrowIfDisposed();
            if (token == null) {
                throw new ArgumentNullException(nameof(token));
            }
            await session.SaveAsync(token);
            await FlushChanges();
        }

        protected override async Task RemoveUserTokenAsync(
            IdentityUserToken token
        ) {
            ThrowIfDisposed();
            if (token == null) {
                throw new ArgumentNullException(nameof(token));
            }
            await session.DeleteAsync(token);
            await FlushChanges();
        }

        public override async Task AddLoginAsync(
            TUser user,
            UserLoginInfo login,
            CancellationToken cancellationToken = new CancellationToken()
        ) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null) {
                throw new ArgumentNullException(nameof(user));
            }
            if (login == null) {
                throw new ArgumentNullException(nameof(login));
            }
            await session.SaveAsync(
                CreateUserLogin(user, login),
                cancellationToken
            );
            await FlushChanges(cancellationToken);
        }

        public override async Task RemoveLoginAsync(
            TUser user,
            string loginProvider,
            string providerKey,
            CancellationToken cancellationToken = new CancellationToken()
        ) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null) {
                throw new ArgumentNullException(nameof(user));
            }
            var login = await FindUserLoginAsync(
                user.Id,
                loginProvider,
                providerKey,
                cancellationToken
            );
            if (login != null) {
                await session.DeleteAsync(login, cancellationToken);
            }
        }

        public override async Task<IList<UserLoginInfo>> GetLoginsAsync(
            TUser user,
            CancellationToken cancellationToken = new CancellationToken()
        ) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null) {
                throw new ArgumentNullException(nameof(user));
            }
            var userId = user.Id;
            var logins = await UserLogins.Where(l => l.UserId.Equals(userId))
                .Select(
                    l => new UserLoginInfo(
                        l.LoginProvider,
                        l.ProviderKey,
                        l.ProviderDisplayName
                    )
                )
                .ToListAsync(cancellationToken);
            return logins;
        }

        public override async Task<TUser> FindByEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken = new CancellationToken()
        ) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            return await Users.FirstOrDefaultAsync(
                u => u.NormalizedEmail == normalizedEmail,
                cancellationToken
            );
        }

        protected async Task FlushChanges(
            CancellationToken cancellationToken = default(CancellationToken)
        ) {
            if (AutoFlushChanges) {
                await session.FlushAsync(cancellationToken);
                session.Clear();
            }
        }

    }
}
