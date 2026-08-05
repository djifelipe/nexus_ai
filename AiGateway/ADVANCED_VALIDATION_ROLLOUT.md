# Advanced response validation rollout

## Scope

Phase 4 validates generated claims against the exact sources included in the prompt, blocks sensitive or unauthorized output, calculates a deterministic grounding score, and can regenerate a correctable response once. It does not add write tools or direct database access.

## Configuration

`AdvancedValidation` controls the rollout:

- `Enabled`: runs the advanced decision pipeline. Disable to use citation validation only.
- `ShadowModeEnabled`: evaluates and records the advanced result but returns the citation validator result.
- `RegenerationEnabled`: permits one regeneration for correctable `PartiallyGrounded` or `InvalidFormat` results.
- `ModelClaimExtractionEnabled`: uses structured model extraction with deterministic fallback.
- `MaxResponseCharacters`, `MaxClaims`, `MaxEvidenceCandidatesPerClaim`: bound work and memory.
- `ExternalTimeoutMs`: bounds optional extraction and semantic dependencies.
- score weights must total `1`; thresholds are validated during startup.
- `PolicyVersion` identifies the decision policy in bounded telemetry.

The default score is:

```text
retrievalCoverage * 0.35 + citationCoverage * 0.25 +
semanticGrounding * 0.25 + intentConfidence * 0.15
```

Security and permission failures override the score.

## Progressive activation

1. Deploy with `Enabled=true`, `ShadowModeEnabled=true`, `RegenerationEnabled=false`.
2. Compare status bands, unsupported-claim counts, false positives, and advanced-validation latency.
3. Disable shadow mode after the acceptance dataset is stable.
4. Enable regeneration and confirm that attempts never exceed one.

## Observability and privacy

The `ai.response.validate` activity and metrics record only bounded aggregates: status, score band, policy version, semantic outcome, claim counts, citation coverage, attempt, regeneration flag, duration, and stable error category. They never record the answer, claims, evidence, prompts, raw detected values, company ID, or user ID as metric labels. Telemetry sink failures do not change validation decisions.

Track basic citation validation and advanced validation separately when evaluating latency. The configured global request timeout remains authoritative.

## Failure behavior

- Missing or unsupported evidence returns `InsufficientKnowledge` or `PartiallyGrounded`.
- External semantic failure returns a conservative review state with `AI_VALIDATION_DEPENDENCY_UNAVAILABLE`.
- Size or claim limits return a stable safe status with `AI_VALIDATION_LIMIT_EXCEEDED`.
- Sensitive data, cross-tenant evidence, permission bypass, SQL, prompts, secrets, or stack traces return `Unsafe` without regeneration.
- Cancellation propagates to claim extraction and grounding and never releases a partial response as grounded.

## Rollback

Set `AdvancedValidation:Enabled` to `false`. This immediately restores the citation-only validator without changing the HTTP response schema. Keep telemetry fields optional during rollback. No data migration is required.
