# Automated Steam Workshop deployment

The release workflow can update existing Workshop item **3735336239** from GitHub-hosted Ubuntu. After a main push passes both builds and all tests, it creates the GitHub release and uploads that run's validated ZIP. Pull requests and manual verification runs never upload. Title and visibility stay unchanged.

Deployment is enabled only when the repository variable `STEAM_WORKSHOP_ENABLED` is `true`. Leave it disabled until the gameplay checks in [MIGRATION_STATUS.md](../MIGRATION_STATUS.md) are complete and the authentication setup below is ready. This switches on uploads for subsequent successful main pushes; it does not replay a past build.

## One-time setup

1. Use a Steam account authorized to update this existing Workshop item. In a private Linux SteamCMD installation, run `./steamcmd.sh +login <username>`, enter the password and Steam Guard code interactively, run `info`, then `quit`. Confirm a second username-only login works without prompting.
2. Copy that installation's `config/config.vdf` into the encrypted environment secret `STEAM_CONFIG_VDF` as base64. Set `STEAM_USERNAME` to the same account name. Use the repository's **Settings → Environments → steam-workshop** page or the CLI below. Never put the login state, password, or Guard code in an issue, PR, or chat.
3. Restrict the environment's deployment branches to `main`. No reviewer requirement is needed for unattended uploads; add one only if you want manual review before each deployment.
4. When gameplay validation is complete, set the repository variable with `gh variable set STEAM_WORKSHOP_ENABLED --repo sayhiben/NoCavesContinued --body true`.

From your authenticated Linux SteamCMD directory, the following sends state directly into GitHub's encrypted secret storage without printing it:

```bash
gh secret set STEAM_USERNAME --repo sayhiben/NoCavesContinued --env steam-workshop
base64 -w 0 config/config.vdf | gh secret set STEAM_CONFIG_VDF \
  --repo sayhiben/NoCavesContinued --env steam-workshop
```

The first command prompts for the username. A Steam password is not stored in GitHub. Treat the saved login state as a credential. [Valve's CI guidance](https://partner.steamgames.com/doc/sdk/uploading#build_account) describes preserving `config/config.vdf` and reusing username-only login; a fresh password login issues a new Steam Guard token.

## Execution and recovery

CI restores the login state into an isolated temporary SteamCMD installation, supplies no interactive input, and limits upload time. The publisher checks the existing item ID and requires a successful upload result for that item. A nonzero exit or missing success confirmation fails the job. Authentication state and private Steam output are removed rather than cached or attached as artifacts.

Steam can revoke or update cached login state. This workflow does not give itself permission to rewrite GitHub secrets, so it cannot preserve a rotated token automatically. If login expires or Steam requests a new Guard authorization, repeat the private interactive login and replace `STEAM_CONFIG_VDF`. Hosted-runner login and live Workshop upload still need validation with your account; mock tests only verify the orchestration.

After correcting a deployment failure, use **Re-run failed jobs** in GitHub Actions so it reuses the already released package. Re-running every job refuses an existing release tag instead of replacing published bytes with a rebuild. If `main` has advanced, the stale-commit check refuses the old upload; use the successful run for the current main commit.

To stop automatic uploads, set `STEAM_WORKSHOP_ENABLED` to `false`. This does not change the Workshop listing or cancel an upload already submitted to Steam. To publish a tested package locally, use the helper documented in [README.md](../README.md#steam-workshop-publishing); never use an upload as a build check.
