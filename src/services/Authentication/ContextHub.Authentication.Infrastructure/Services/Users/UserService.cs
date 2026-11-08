namespace ContextHub.Authentication.Infrastructure.Services.Users;

/// <summary>
/// Coordinates account and membership invariants inside the native Identity transaction boundary.
/// </summary>
/// <param name="userManager">The native account manager.</param>
/// <param name="userRepository">The specific account-query repository.</param>
/// <param name="unitOfWork">The shared transaction owner for the same scoped context.</param>
/// <param name="authenticationOptions">The trusted issuer used for invitation links.</param>
/// <param name="sessionService">The native human-grant lifecycle boundary.</param>
/// <param name="roleService">The provisioned role catalog; authorization never uses its metadata cache.</param>
/// <param name="logger">The safe account-operation logger.</param>
public sealed class UserService(
    UserManager<User> userManager,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IOptions<AuthenticationOptions> authenticationOptions,
    ISessionService sessionService,
    IRoleService roleService,
    ILogger<UserService> logger) : IUserService
{
    /// <summary>
    /// Delegates account reads to the feature repository without rebuilding persistence queries in this service.
    /// </summary>
    /// <param name="request">The grouped list inputs.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The approved account page.</returns>
    public Task<PageResponse<UserResponseDto>> GetPageAsync(
        GetUsersRequestDto request,
        CancellationToken cancellationToken) =>
        userRepository.GetPageAsync(request, cancellationToken);

    /// <summary>
    /// Gets the repository projection needed by native account operation responses.
    /// </summary>
    /// <param name="id">The account identifier.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The approved projection, or null for Application to classify as unavailable.</returns>
    public Task<UserResponseDto?> GetAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        userRepository.GetAsync(id, cancellationToken);

    /// <summary>
    /// Uses native password verification and commits the changed credential with human-grant revocation.
    /// </summary>
    /// <param name="id">The authenticated subject, never a request-body identifier.</param>
    /// <param name="request">Current credential proof and the replacement password.</param>
    /// <param name="cancellationToken">Cancels the shared transaction and account reads.</param>
    /// <returns>The approved profile after a successful credential change.</returns>
    public async Task<UserResponseDto> ChangePasswordAsync(
        Guid id,
        ChangePasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        // Commit password change and grant revocation together.
        var profile = await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                // Recheck active Owner access inside the write transaction.
                var user = await userRepository.FirstOrDefaultAsync(
                    predicate: value => value.Id == id && value.IsActive,
                    cancellationToken: token);
                if (user is null || !await userManager.IsInRoleAsync(user, AuthenticationConstants.OWNER))
                {
                    logger.LogWarning(
                        AuthenticationLogConstants.OPERATION_REJECTED,
                        nameof(ChangePasswordAsync),
                        AuthenticationErrorConstants.PASSWORD_CHANGE_REJECTED);
                    throw new BusinessException(
                        AuthenticationErrorConstants.PASSWORD_CHANGE_REJECTED,
                        AuthenticationErrorConstants.ACCOUNT_OPERATION_REJECTED);
                }

                // Identity checks the old password, enforces policy and updates the security stamp once.
                var result = await userManager.ChangePasswordAsync(
                    user,
                    request.CurrentPassword,
                    request.NewPassword);
                if (!result.Succeeded)
                {
                    logger.LogWarning(
                        AuthenticationLogConstants.OPERATION_REJECTED,
                        nameof(ChangePasswordAsync),
                        AuthenticationErrorConstants.PASSWORD_CHANGE_REJECTED);
                    throw new BusinessException(
                        AuthenticationErrorConstants.PASSWORD_CHANGE_REJECTED,
                        AuthenticationErrorConstants.ACCOUNT_OPERATION_REJECTED);
                }

                // Revoke human grants without changing independent client credentials.
                await sessionService.RevokeAllAsync(user.Id, token);

                // Project the saved account within this transaction.
                return (await userRepository.GetAsync(user.Id, token))!;
            },
            cancellationToken);

        // Log the security outcome after commit without credential data.
        logger.LogInformation(AuthenticationLogConstants.PASSWORD_CHANGED);

        return profile;
    }

    /// <summary>
    /// Creates an Owner through native password validation and persists role membership in the same transaction.
    /// </summary>
    /// <param name="request">The credentials accepted by the Application validator.</param>
    /// <param name="cancellationToken">Cancels native persistence.</param>
    /// <returns>The safe projection after successful native creation.</returns>
    public async Task<UserResponseDto> CreateAsync(
        CreateUserRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Keep Identity account and Owner-membership saves atomic.
            return await unitOfWork.ExecuteInTransactionAsync(
                async token =>
                {
                    // Serialize role-sensitive changes through the existing repository boundary.
                    await userRepository.LockOwnerMembershipAsync(token);

                    // Map profile fields; identity and lockout state remain server-owned.
                    var user = request.Adapt<User>();
                    user.Id = Guid.CreateVersion7();
                    user.LockoutEnabled = true;

                    // Identity owns password policy, normalization and credential persistence.
                    var created = await userManager.CreateAsync(user, request.Password);
                    if (!created.Succeeded)
                    {
                        // Classify duplicate email without exposing native error descriptions.
                        var duplicateEmail = created.Errors.Any(error =>
                            error.Code == AuthenticationInfrastructureConstants.NATIVE_DUPLICATE_EMAIL);
                        var errorCode = duplicateEmail ? AuthenticationErrorConstants.ACCOUNT_EMAIL_CONFLICT
                            : AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED;
                        logger.LogWarning(
                            AuthenticationLogConstants.OPERATION_REJECTED,
                            nameof(CreateAsync),
                            errorCode);
                        throw new BusinessException(
                            errorCode,
                            AuthenticationErrorConstants.ACCOUNT_OPERATION_REJECTED,
                            duplicateEmail ? BusinessErrorKind.Conflict : BusinessErrorKind.Invalid);
                    }

                    // The server assigns Owner; the request cannot choose roles or copy credential state.
                    var assigned = await userManager.AddToRoleAsync(user, AuthenticationConstants.OWNER);
                    if (!assigned.Succeeded)
                    {
                        logger.LogWarning(
                            AuthenticationLogConstants.OPERATION_REJECTED,
                            nameof(CreateAsync),
                            AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED);
                        throw new BusinessException(
                            AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED,
                            AuthenticationErrorConstants.ACCOUNT_OPERATION_REJECTED);
                    }

                    // Return the repository projection rather than the Identity entity, so hashes and stamps remain inside Infrastructure.
                    return (await userRepository.GetAsync(user.Id, token))!;
                },
                cancellationToken);
        }
        catch (DbUpdateException exception) when (
            PostgreSqlExceptionHelper.GetUniqueConstraintName(exception) == AuthenticationInfrastructureConstants.UNIQUE_EMAIL_INDEX)
        {
            // Native validation handles normal duplicates; this translation covers the concurrent database uniqueness race.
            logger.LogWarning(
                AuthenticationLogConstants.OPERATION_REJECTED,
                nameof(CreateAsync),
                AuthenticationErrorConstants.ACCOUNT_EMAIL_CONFLICT);
            throw new BusinessException(
                AuthenticationErrorConstants.ACCOUNT_EMAIL_CONFLICT,
                AuthenticationErrorConstants.ACCOUNT_POLICY_CONFLICT,
                BusinessErrorKind.Conflict);
        }
    }

    /// <summary>
    /// Creates a passwordless administrative account and a bounded invitation in one transaction.
    /// </summary>
    /// <param name="request">The account metadata and initial role set.</param>
    /// <param name="cancellationToken">Cancels provisioning.</param>
    /// <returns>The approved user and one-time setup URL.</returns>
    public async Task<InvitedUserResponseDto> InviteAsync(
        InviteUserRequestDto request,
        CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                // Keep role checks and passwordless account creation under the same membership lock.
                await userRepository.LockOwnerMembershipAsync(token);

                // Check requested roles against the live native catalog, not the cached display metadata.
                await ValidateRolesAsync(request.RoleNames);

                // Mapping handles approved metadata; callers cannot supply the generated key or credential policy.
                var user = request.Adapt<User>();
                user.Id = Guid.CreateVersion7();
                user.LockoutEnabled = true;

                // Create a passwordless account explicitly; the separate invitation flow will bind its first password later.
                var createResult = await userManager.CreateAsync(user);

                if (!createResult.Succeeded)
                {
                    logger.LogWarning(
                        AuthenticationLogConstants.OPERATION_REJECTED,
                        nameof(InviteAsync),
                        AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED);
                    throw new BusinessException(
                        AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED,
                        AuthenticationErrorConstants.ACCOUNT_OPERATION_REJECTED);
                }

                // Assign only validated role names before producing a usable invitation; any failure aborts the surrounding transaction.
                var roleAssignmentResult = await userManager.AddToRolesAsync(user, request.RoleNames);

                if (!roleAssignmentResult.Succeeded)
                {
                    logger.LogWarning(
                        AuthenticationLogConstants.OPERATION_REJECTED,
                        nameof(InviteAsync),
                        AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED);
                    throw new BusinessException(
                        AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED,
                        AuthenticationErrorConstants.ACCOUNT_OPERATION_REJECTED);
                }

                // Generate the native invitation only after account and membership writes have succeeded.
                return await CreateInvitationAsync(user, token);
            },
            cancellationToken);

    /// <summary>
    /// Changes status while serializing every operation that could remove the final active Owner.
    /// </summary>
    /// <param name="id">The account identifier.</param>
    /// <param name="request">The desired state and expected version.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The latest approved account state.</returns>
    public Task<UserResponseDto> SetStatusAsync(
        Guid id,
        UserStatusRequestDto request,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                // Serialize membership-sensitive changes before checking the current version and final-Owner invariant.
                await userRepository.LockOwnerMembershipAsync(token);
                var user = await RequireVersionAsync(id, request.ConcurrencyToken, token);

                // Protect the last active Owner only when this request would deactivate one; repeated no-op updates do not remove access again.
                if (!request.IsActive && user.IsActive && await userManager.IsInRoleAsync(
                    user,
                    AuthenticationConstants.OWNER))
                {
                    await EnsureAnotherOwnerAsync(id, token);
                }

                // Write only a real state transition, keeping the stored concurrency version stable for a no-op request.
                if (user.IsActive != request.IsActive)
                {
                    user.IsActive = request.IsActive;

                    // Persist the active flag through Identity so its validation and optimistic concurrency checks remain authoritative.
                    var updateResult = await userManager.UpdateAsync(user);

                    if (!updateResult.Succeeded)
                    {
                        logger.LogWarning(
                            AuthenticationLogConstants.OPERATION_REJECTED,
                            nameof(SetStatusAsync),
                            AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED);
                        throw new BusinessException(
                            AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED,
                            AuthenticationErrorConstants.ACCOUNT_OPERATION_REJECTED);
                    }

                    // Invalidate existing cookie validation state before committing the status change and human-token revocation.
                    var securityStampResult = await userManager.UpdateSecurityStampAsync(user);

                    if (!securityStampResult.Succeeded)
                    {
                        logger.LogWarning(
                            AuthenticationLogConstants.OPERATION_REJECTED,
                            nameof(SetStatusAsync),
                            AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED);
                        throw new BusinessException(
                            AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED,
                            AuthenticationErrorConstants.ACCOUNT_OPERATION_REJECTED);
                    }

                    // Previously issued human grants must not retain authority after account status changes.
                    await sessionService.RevokeAllAsync(user.Id, token);
                }

                // Read the saved profile in this transaction, including inactive state.
                return (await userRepository.GetAsync(id, token))!;
            },
            cancellationToken);

    /// <summary>
    /// Replaces native memberships and invalidates old authorization state atomically.
    /// </summary>
    /// <param name="id">The account identifier.</param>
    /// <param name="request">The desired complete membership set.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The updated account projection.</returns>
    public Task<UserResponseDto> ReplaceRolesAsync(
        Guid id,
        UserRolesRequestDto request,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                // Keep the final-Owner check and membership changes inside the same locked transaction.
                await userRepository.LockOwnerMembershipAsync(token);
                var requestedRoles = await userRepository.ResolveRoleNamesAsync(request.RoleIds, token);
                var user = await RequireVersionAsync(id, request.ConcurrencyToken, token);
                var current = await userManager.GetRolesAsync(user);

                // Keep another active Owner when removing this account's Owner role.
                if (user.IsActive && current.Contains(AuthenticationConstants.OWNER) &&
                    !requestedRoles.Contains(AuthenticationConstants.OWNER))
                {
                    await EnsureAnotherOwnerAsync(id, token);
                }

                // Membership order alone must not rewrite the account or revoke sessions.
                if (!current.Order().SequenceEqual(requestedRoles.Order()))
                {
                    // Remove only memberships absent from the requested set.
                    var roleRemovalResult = await userManager.RemoveFromRolesAsync(
                        user,
                        current.Except(requestedRoles));

                    if (!roleRemovalResult.Succeeded)
                    {
                        logger.LogWarning(
                            AuthenticationLogConstants.OPERATION_REJECTED,
                            nameof(ReplaceRolesAsync),
                            AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED);
                        throw new BusinessException(
                            AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED,
                            AuthenticationErrorConstants.ACCOUNT_OPERATION_REJECTED);
                    }

                    // Commit membership additions and removals atomically.
                    var roleAdditionResult = await userManager.AddToRolesAsync(
                        user,
                        requestedRoles.Except(current));

                    if (!roleAdditionResult.Succeeded)
                    {
                        logger.LogWarning(
                            AuthenticationLogConstants.OPERATION_REJECTED,
                            nameof(ReplaceRolesAsync),
                            AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED);
                        throw new BusinessException(
                            AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED,
                            AuthenticationErrorConstants.ACCOUNT_OPERATION_REJECTED);
                    }

                    // Invalidate stale cookies and grants only after a real role change.
                    var securityStampResult = await userManager.UpdateSecurityStampAsync(user);

                    if (!securityStampResult.Succeeded)
                    {
                        logger.LogWarning(
                            AuthenticationLogConstants.OPERATION_REJECTED,
                            nameof(ReplaceRolesAsync),
                            AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED);
                        throw new BusinessException(
                            AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED,
                            AuthenticationErrorConstants.ACCOUNT_OPERATION_REJECTED);
                    }

                    // Require fresh grants after role claims change.
                    await sessionService.RevokeAllAsync(id, token);
                }

                // Project the current memberships from persistence instead of echoing the requested role list.
                return (await userRepository.GetAsync(id, token))!;
            },
            cancellationToken);

    /// <summary>
    /// Invalidates the previous invitation before returning a new one.
    /// </summary>
    /// <param name="id">The account awaiting enrollment.</param>
    /// <param name="request">The latest account version.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The new one-time invitation.</returns>
    public Task<InvitedUserResponseDto> RegenerateInvitationAsync(
        Guid id,
        InvitationRequestDto request,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                // Reject stale invitation requests before changing account state.
                var user = await RequireVersionAsync(id, request.ConcurrencyToken, token);

                // Invitation setup is limited to active passwordless accounts.
                if (!user.IsActive || await userManager.HasPasswordAsync(user))
                {
                    logger.LogWarning(
                        AuthenticationLogConstants.OPERATION_REJECTED,
                        nameof(RegenerateInvitationAsync),
                        AuthenticationErrorConstants.INVITATION_STATE_INVALID);
                    throw new BusinessException(
                        AuthenticationErrorConstants.INVITATION_STATE_INVALID,
                        AuthenticationErrorConstants.SETUP_UNAVAILABLE,
                        BusinessErrorKind.Conflict);
                }

                // Rotate the native stamp to invalidate the previous invitation.
                var securityStampResult = await userManager.UpdateSecurityStampAsync(user);

                if (!securityStampResult.Succeeded)
                {
                    logger.LogWarning(
                        AuthenticationLogConstants.OPERATION_REJECTED,
                        nameof(RegenerateInvitationAsync),
                        AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED);
                    throw new BusinessException(
                        AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED,
                        AuthenticationErrorConstants.ACCOUNT_OPERATION_REJECTED);
                }

                // Create the replacement invitation after rotating its native stamp.
                return await CreateInvitationAsync(user, token);
            },
            cancellationToken);

    /// <summary>
    /// Counts current database membership under the invariant lock rather than trusting token claims.
    /// </summary>
    /// <param name="excluded">The account being changed.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The invariant verification.</returns>
    private async Task EnsureAnotherOwnerAsync(
        Guid excluded,
        CancellationToken cancellationToken)
    {
        // Read live memberships while the caller's invariant lock is held.
        var exists = await userRepository.HasAnotherActiveOwnerAsync(excluded, cancellationToken);

        if (!exists)
        {
            logger.LogWarning(
                AuthenticationLogConstants.OPERATION_REJECTED,
                nameof(EnsureAnotherOwnerAsync),
                AuthenticationErrorConstants.LAST_ACTIVE_OWNER);
            throw new BusinessException(
                AuthenticationErrorConstants.LAST_ACTIVE_OWNER,
                AuthenticationErrorConstants.ACCOUNT_POLICY_CONFLICT,
                BusinessErrorKind.Conflict);
        }
    }

    /// <summary>
    /// Checks the client-supplied concurrency value before any Identity mutation.
    /// </summary>
    /// <param name="id">The target account.</param>
    /// <param name="version">The expected opaque version.</param>
    /// <param name="cancellationToken">Cancels the repository's entity query.</param>
    /// <returns>The tracked native user.</returns>
    private async Task<User> RequireVersionAsync(
        Guid id,
        string version,
        CancellationToken cancellationToken)
    {
        // Query entities through the repository; credential mutations remain with Identity.
        var user = await userRepository.FirstOrDefaultAsync(
            predicate: value => value.Id == id,
            cancellationToken: cancellationToken);

        // A missing account remains a not-found outcome.
        if (user is null)
        {
            logger.LogWarning(
                AuthenticationLogConstants.OPERATION_REJECTED,
                nameof(RequireVersionAsync),
                AuthenticationErrorConstants.USER_MISSING);
            throw new BusinessException(
                AuthenticationErrorConstants.USER_MISSING,
                AuthenticationErrorConstants.ACCOUNT_UNAVAILABLE,
                BusinessErrorKind.NotFound);
        }

        // Reject stale edits before mutation; Identity also checks the stamp during save.
        if (!string.Equals(user.ConcurrencyStamp, version, StringComparison.Ordinal))
        {
            logger.LogWarning(
                AuthenticationLogConstants.OPERATION_REJECTED,
                nameof(RequireVersionAsync),
                AuthenticationErrorConstants.CONCURRENCY_CONFLICT);
            throw new BusinessException(
                AuthenticationErrorConstants.CONCURRENCY_CONFLICT,
                AuthenticationErrorConstants.ACCOUNT_CHANGED,
                BusinessErrorKind.Conflict);
        }

        return user;
    }

    /// <summary>
    /// Accepts only explicitly provisioned, distinct role names.
    /// </summary>
    /// <param name="names">The desired roles.</param>
    /// <returns>The validation operation.</returns>
    private async Task ValidateRolesAsync(string[]? names)
    {
        // Reject an absent or duplicate selection before native membership changes begin.
        if (names is null || names.Distinct(StringComparer.Ordinal).Count() != names.Length)
        {
            logger.LogWarning(
                AuthenticationLogConstants.OPERATION_REJECTED,
                nameof(ValidateRolesAsync),
                AuthenticationErrorConstants.ROLES_INVALID);
            throw new BusinessException(
                    AuthenticationErrorConstants.ROLES_INVALID,
                    AuthenticationErrorConstants.INVALID_ROLE_SELECTION);
        }

        // Validate against provisioned roles rather than a hard-coded enum.
        foreach (var name in names)
        {
            if (!await roleService.ExistsAsync(name))
            {
                logger.LogWarning(
                    AuthenticationLogConstants.OPERATION_REJECTED,
                    nameof(ValidateRolesAsync),
                    AuthenticationErrorConstants.ROLES_INVALID);
                throw new BusinessException(
                    AuthenticationErrorConstants.ROLES_INVALID,
                    AuthenticationErrorConstants.INVALID_ROLE_SELECTION);
            }
        }
    }

    /// <summary>
    /// Binds a native invitation token to the account's current security stamp.
    /// </summary>
    /// <param name="user">The account awaiting initial password setup.</param>
    /// <param name="cancellationToken">Cancels response projection.</param>
    /// <returns>The one-time URL and its declared expiry.</returns>
    private async Task<InvitedUserResponseDto> CreateInvitationAsync(
        User user,
        CancellationToken cancellationToken)
    {
        // Keep invitation proof separate from password-reset OTP.
        var invitation = await userManager.GenerateUserTokenAsync(
            user,
            AuthenticationConstants.INVITATION_PROVIDER,
            AuthenticationConstants.INVITATION_PURPOSE);

        // Build the invitation from the trusted issuer without logging its secret.
        var url = authenticationOptions.Value.Issuer.TrimEnd('/') + AuthenticationConstants.Endpoints.INVITATION_PAGE + "?userId=" + user.Id +
            "&token=" + Uri.EscapeDataString(invitation);

        // Return public account and invitation metadata only.
        return new(
            (await userRepository.GetAsync(user.Id, cancellationToken))!,
            url,
            DateTimeOffset.UtcNow.AddMinutes(AuthenticationInfrastructureConstants.INVITATION_LIFETIME_MINUTES));
    }

}
