## Branch Naming Conventions

To keep our repository clean and our CI/CD pipelines running smoothly, please adhere to the following branch naming conventions. Branches that do not follow this format will be rejected by the repository rules.

**Format:** `type/short-description`

**Valid Types:**
- `feat/`: New features
- `fix/`: Bug fixes
- `docs/`: Documentation updates
- `chore/`: Maintenance, CI/CD, or dependency updates
- `refactor/`: Code structure improvements
- `test/`: Adding or fixing tests

**Examples:**
- ✅ `feat/user-authentication`
- ✅ `fix/nav-bar-overlap`
- ❌ `feature/UserAuth` (use `feat/` and lowercase)
- ❌ `fix_nav_bar` (use hyphens, not underscores)

## Pull Request Guidelines

1. Ensure your branch is up-to-date with `master` before submitting.
2. All automated CI/CD checks (Build, WriterSide Test) must pass.
3. At least one code review approval from a team member is required to merge.
4. Fill out the Pull Request template completely.
