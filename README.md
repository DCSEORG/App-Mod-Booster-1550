![Header image](https://github.com/DougChisholm/App-Mod-Booster/blob/main/repo-header-booster.png)

# App-Mod-Booster
A project to show how GitHub coding agent can turn screenshots of a legacy app into a working proof-of-concept for a cloud native Azure replacement if the legacy database schema is also provided.

Steps to modernise an app:

1. Fork this repo 
2. In new repo replace the screenshots and sql schema (or keep the samples)
3. Open the coding agent and use app-mod-booster agent telling it "modernise my app"
4. When the app code is generated (can take up to 30 minutes) there will be a pull request to approve.
5. Now you can use codespaces to deploy the app to azure (or open VS Code and clone the repo locally - you will need to install some tools locally or use the devcontainer)
6. Open terminal and type "az login" to set subscription/context
7. Then type "bash deploy.sh" to deploy the app and db or "bash deploy-with-chat.sh" to deploy the app, db and chat UI.

Supporting slides for Microsoft Employees:
[Here](<https://microsofteur-my.sharepoint.com/:p:/g/personal/dchisholm_microsoft_com/IQAY41LQ12fjSIfFz3ha4hfFAZc7JQQuWaOrF7ObgxRK6f4?e=p6arJs>)

---

## For Contributors / Coding Agents — Git Push Token Fix

### The Problem
When running `git push` directly inside the coding agent's bash tool, the push fails with an error like:

```
remote: Permission to DCSEORG/App-Mod-Booster-1550.git denied to <username>.
fatal: unable to access '...': The requested URL returned error: 403
```

**Why this happens:** The `GITHUB_TOKEN` environment variable available to the bash shell is a personal OAuth token (prefixed `ghu_`) that only has **read** access to the repository. It cannot push.

### The Fix
The GitHub MCP server holds a **separate** app installation token (prefixed `ghs_`, belonging to `copilot-swe-agent[bot]`) that **does** have write access. This token is never exposed as an environment variable — it is only accessible through the MCP tool layer.

**Always use the `report_progress` tool to commit and push changes.** It internally uses the correct `ghs_` installation token and will succeed where a bare `git push` in bash fails.

```
# ❌ This will fail with a 403 / "denied" error:
git push origin my-branch

# ✅ This works — use the report_progress MCP tool instead.
# It runs git add, git commit, and git push using the correct token automatically.
```

If you already have local commits that haven't been pushed yet, call `report_progress` with any description update to trigger the push via the correct token. Do **not** attempt to extract tokens from process environments or `.git/config` as a workaround.
