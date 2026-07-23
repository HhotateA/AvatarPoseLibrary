const EVENTS = Object.freeze({
  SESSION_STARTED: "apl_editor_session_started",
  VERSION_CHANGED: "apl_version_changed",
  BUILD_COMPLETED: "apl_build_completed",
  BUILD_FAILED: "apl_build_failed",
});

const REQUEST_TYPES = Object.freeze({
  ERROR_REPORT: "error_report",
});

const POST_EVENTS = Object.freeze([
  EVENTS.VERSION_CHANGED,
  EVENTS.BUILD_COMPLETED,
  EVENTS.BUILD_FAILED,
]);

const FIELDS = Object.freeze({
  MINIMAL: [
    "schema_version", "telemetry_mode", "event_name", "client_id", "apl_version",
  ],
  ENVIRONMENT: [
    "event_id", "unity_version", "vrcsdk_version", "ndmf_version",
    "session_id", "engagement_time_msec",
  ],
  BUILD: [
    "previous_apl_version", "build_duration_ms", "component_count",
    "library_count", "category_count", "pose_count", "humanoid",
    "audio_enabled", "locomotion_enabled", "fx_enabled", "cache_enabled",
    "auto_reset_enabled", "build_stage", "exception_type",
  ],
  ERROR_REPORT: [
    "schema_version", "report_id", "occurred_at_utc", "client_id", "apl_version",
    "unity_version", "vrcsdk_version", "ndmf_version", "editor_platform",
    "build_stage", "build_duration_ms", "exception_type", "error_text",
    "error_text_truncated", "stack_frames", "component_count", "library_count",
    "category_count", "pose_count", "humanoid", "libraries",
  ],
  ERROR_LIBRARY: [
    "category_count", "pose_count", "enable_height", "enable_speed",
    "enable_mirror", "enable_tracking", "enable_deep_sync", "enable_pose_space",
    "enable_cache", "enable_auto_reset", "enable_locomotion", "enable_fx",
    "suppress_additive", "audio_enabled", "write_defaults",
  ],
  ERROR_LIBRARY_FLAGS: [
    "enable_height", "enable_speed", "enable_mirror", "enable_tracking",
    "enable_deep_sync", "enable_pose_space", "enable_cache", "enable_auto_reset",
    "enable_locomotion", "enable_fx", "suppress_additive", "audio_enabled",
  ],
});

const SESSION_FIELDS = FIELDS.MINIMAL.concat(FIELDS.ENVIRONMENT);
const EVENT_FIELDS = SESSION_FIELDS.concat(FIELDS.BUILD);
const ERROR_REPORT_ALLOWED_FIELDS = FIELDS.ERROR_REPORT.concat(["request_type"]);
const GET_INTEGER_FIELDS = [
  "schema_version", "session_id", "engagement_time_msec",
];
const PATTERNS = Object.freeze({
  ID: /^[a-f0-9]{32}$/,
  VERSION: /^[0-9A-Za-z][0-9A-Za-z._+\-]{0,63}$/,
  PLATFORM: /^[A-Za-z0-9_]{1,40}$/,
  BUILD_STAGE: /^[a-z0-9_]{0,40}$/,
  ERROR_BUILD_STAGE: /^[a-z0-9_]{1,40}$/,
  EVENT_EXCEPTION: /^[A-Za-z0-9_.+`]{0,100}$/,
  WRITE_DEFAULTS: /^[A-Za-z0-9_]{1,40}$/,
  DISCORD_CHANNEL_ID: /^[0-9]{17,20}$/,
  ISO_UTC: /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z$/,
});

const DEFAULTS = Object.freeze({
  SCHEMA_VERSION: 1,
  MAX_LOG_BYTES: 8192,
  MAX_ERROR_BYTES: 65536,
  MAX_ERROR_TEXT_CHARACTERS: 24000,
  MAX_STACK_FRAMES: 32,
  LEGACY_CLIENT_ID: "00000000000000000000000000000000",
  LEGACY_APL_VERSION: "1.2.35",
});

function doGet(e) {
  const version = latestVersion_();
  if (version === null) {
    return jsonResponse_({ error: "version_sheet_not_found" });
  }

  let telemetryResult;
  try {
    telemetryResult = handleSession_(e, settings_());
  } catch (_) {
    // Telemetry must never break the version endpoint.
    telemetryResult = failure_("internal_error");
  }

  const output = {
    version: version,
    telemetry_ok: telemetryResult.ok,
  };
  if (!telemetryResult.ok) {
    output.telemetry_error = telemetryResult.error;
  }
  return jsonResponse_(output);
}

function doPost(e) {
  try {
    return jsonResponse_(handlePost_(e, settings_()));
  } catch (_) {
    return jsonResponse_(failure_("internal_error"));
  }
}

function handleSession_(e, settings) {
  const input = sessionInputFromGet_(e);
  if (input === null) {
    return sendGa4_(legacyGa4Payload_(settings), settings);
  }
  if (!validateSession_(input, settings.schemaVersion)) {
    return failure_("invalid_request");
  }
  return sendGa4_(ga4Payload_(input), settings);
}

function handlePost_(e, settings) {
  const maximumBytes = Math.max(settings.maxLogBytes, settings.maxErrorBytes);
  const request = jsonPostInput_(e, maximumBytes);
  if (request === null) {
    return failure_("invalid_request");
  }

  if (isErrorReport_(request.value)) {
    return handleErrorReport_(request, settings);
  }
  return handleTelemetryEvent_(request, settings);
}

function handleTelemetryEvent_(request, settings) {
  if (request.byteLength > settings.maxLogBytes
      || !validatePostEvent_(request.value, settings.schemaVersion)) {
    return failure_("invalid_request");
  }
  return sendGa4_(ga4Payload_(request.value), settings);
}

function handleErrorReport_(request, settings) {
  if (request.byteLength > settings.maxErrorBytes
      || !validateErrorReport_(request.value, settings)) {
    return failure_("invalid_request");
  }
  return sendErrorReport_(request.value, settings);
}

function latestVersion_() {
  const sheet = SpreadsheetApp
    .getActiveSpreadsheet()
    .getSheetByName("Version");
  return sheet === null
    ? null
    : String(sheet.getRange("A1").getValue() || "");
}

function settings_() {
  const properties = PropertiesService.getScriptProperties();
  const integer = (name, fallback) =>
    positiveInteger_(properties.getProperty(name), fallback);
  return {
    measurementId: properties.getProperty("GA4_MEASUREMENT_ID"),
    apiSecret: properties.getProperty("GA4_API_SECRET"),
    schemaVersion: integer("SCHEMA_VERSION", DEFAULTS.SCHEMA_VERSION),
    maxLogBytes: integer("MAX_REQUEST_BYTES", DEFAULTS.MAX_LOG_BYTES),
    maxErrorBytes: integer("MAX_ERROR_REQUEST_BYTES", DEFAULTS.MAX_ERROR_BYTES),
    maxErrorTextCharacters: integer(
      "MAX_ERROR_TEXT_CHARACTERS",
      DEFAULTS.MAX_ERROR_TEXT_CHARACTERS
    ),
    maxStackFrames: integer("MAX_STACK_FRAMES", DEFAULTS.MAX_STACK_FRAMES),
    discordBotToken: properties.getProperty("DISCORD_BOT_TOKEN"),
    discordChannelId: properties.getProperty("DISCORD_CHANNEL_ID"),
    debugMode: String(properties.getProperty("GA4_DEBUG_MODE")).toLowerCase()
      === "true",
    legacyClientId: properties.getProperty("GA4_LEGACY_CLIENT_ID")
      || DEFAULTS.LEGACY_CLIENT_ID,
  };
}
function sessionInputFromGet_(e) {
  const parameters = e && isObject_(e.parameter) ? e.parameter : {};
  if (!hasFields_(parameters, FIELDS.MINIMAL)) {
    return null;
  }

  const input = Object.assign({}, parameters);
  GET_INTEGER_FIELDS.forEach((field) => {
    if (hasOwn_(input, field)) {
      input[field] = Number(input[field]);
    }
  });
  return input;
}

function jsonPostInput_(e, maximumBytes) {
  if (!e || !e.postData
      || typeof e.postData.contents !== "string"
      || !isJsonContentType_(e.postData.type)
      || Number(e.contentLength || e.postData.length || 0) > maximumBytes) {
    return null;
  }

  try {
    const byteLength = utf8Length_(e.postData.contents);
    if (byteLength > maximumBytes) {
      return null;
    }
    return {
      value: JSON.parse(e.postData.contents),
      byteLength: byteLength,
    };
  } catch (_) {
    return null;
  }
}

function isJsonContentType_(value) {
  return String(value || "").toLowerCase().startsWith("application/json");
}

function utf8Length_(value) {
  return Utilities.newBlob(value).getBytes().length;
}

function validateSession_(input, expectedSchema) {
  if (!validateCommonEvent_(input, expectedSchema, [EVENTS.SESSION_STARTED])) {
    return false;
  }

  const fields = input.telemetry_mode === "detailed"
    ? SESSION_FIELDS
    : FIELDS.MINIMAL;
  return hasExactFields_(input, fields)
    && (input.telemetry_mode === "minimal" || validateEnvironment_(input));
}

function validatePostEvent_(input, expectedSchema) {
  if (!validateCommonEvent_(input, expectedSchema, POST_EVENTS)) {
    return false;
  }

  const fields = input.telemetry_mode === "detailed"
    ? EVENT_FIELDS
    : FIELDS.MINIMAL;
  return hasExactFields_(input, fields)
    && (input.telemetry_mode === "minimal"
        || (validateEnvironment_(input) && validateBuildFields_(input)));
}

function validateCommonEvent_(input, expectedSchema, allowedEvents) {
  return isObject_(input)
    && input.schema_version === expectedSchema
    && ["minimal", "detailed"].includes(input.telemetry_mode)
    && allowedEvents.includes(input.event_name)
    && matches_(input.client_id, PATTERNS.ID)
    && isVersion_(input.apl_version);
}

function validateEnvironment_(input) {
  return matches_(input.event_id, PATTERNS.ID)
    && isVersion_(input.unity_version)
    && isVersion_(input.vrcsdk_version)
    && isVersion_(input.ndmf_version)
    && integerInRange_(input.session_id, 1, Number.MAX_SAFE_INTEGER)
    && integerInRange_(input.engagement_time_msec, 1, 3600000);
}

function validateBuildFields_(input) {
  return optionalVersion_(input.previous_apl_version)
    && integerInRange_(input.build_duration_ms, 0, 86400000)
    && integerInRange_(input.component_count, 0, 10000)
    && integerInRange_(input.library_count, 0, 10000)
    && integerInRange_(input.category_count, 0, 100000)
    && integerInRange_(input.pose_count, 0, 1000000)
    && binary_(input.humanoid)
    && binary_(input.audio_enabled)
    && binary_(input.locomotion_enabled)
    && binary_(input.fx_enabled)
    && binary_(input.cache_enabled)
    && binary_(input.auto_reset_enabled)
    && matches_(input.build_stage, PATTERNS.BUILD_STAGE)
    && matches_(input.exception_type, PATTERNS.EVENT_EXCEPTION);
}

function isErrorReport_(input) {
  return isObject_(input)
    && (input.request_type === REQUEST_TYPES.ERROR_REPORT
        || hasOwn_(input, "report_id"));
}

function validateErrorReport_(input, settings) {
  if (!hasRequiredAndAllowedFields_(
      input,
      FIELDS.ERROR_REPORT,
      ERROR_REPORT_ALLOWED_FIELDS)) {
    return false;
  }

  return (!hasOwn_(input, "request_type")
      || input.request_type === REQUEST_TYPES.ERROR_REPORT)
    && input.schema_version === settings.schemaVersion
    && matches_(input.report_id, PATTERNS.ID)
    && isIsoUtc_(input.occurred_at_utc)
    && matches_(input.client_id, PATTERNS.ID)
    && isVersion_(input.apl_version)
    && isVersion_(input.unity_version)
    && isVersion_(input.vrcsdk_version)
    && isVersion_(input.ndmf_version)
    && matches_(input.editor_platform, PATTERNS.PLATFORM)
    && matches_(input.build_stage, PATTERNS.ERROR_BUILD_STAGE)
    && stringInRange_(input.exception_type, 1, 256)
    && stringInRange_(input.error_text, 1, settings.maxErrorTextCharacters)
    && binary_(input.error_text_truncated)
    && integerInRange_(input.build_duration_ms, 0, 86400000)
    && integerInRange_(input.component_count, 0, 10000)
    && integerInRange_(input.library_count, 0, 10000)
    && integerInRange_(input.category_count, 0, 100000)
    && integerInRange_(input.pose_count, 0, 1000000)
    && binary_(input.humanoid)
    && validateStackFrames_(input.stack_frames, settings.maxStackFrames)
    && Array.isArray(input.libraries)
    && input.libraries.length <= 64
    && input.libraries.every(validateErrorLibrary_);
}

function validateStackFrames_(frames, maximum) {
  return Array.isArray(frames)
    && frames.length <= maximum
    && frames.every((frame) => stringInRange_(frame, 1, 512));
}

function validateErrorLibrary_(library) {
  return hasExactFields_(library, FIELDS.ERROR_LIBRARY)
    && integerInRange_(library.category_count, 0, 100000)
    && integerInRange_(library.pose_count, 0, 1000000)
    && matches_(library.write_defaults, PATTERNS.WRITE_DEFAULTS)
    && FIELDS.ERROR_LIBRARY_FLAGS.every((field) => binary_(library[field]));
}

function ga4Payload_(input) {
  const params = {
    schema_version: input.schema_version,
    telemetry_mode: input.telemetry_mode,
    apl_version: input.apl_version,
    engagement_time_msec: input.telemetry_mode === "detailed"
      ? input.engagement_time_msec
      : 1,
  };

  if (input.telemetry_mode === "detailed") {
    copyFields_(params, input, FIELDS.ENVIRONMENT);
    appendEventSpecificGa4Params_(params, input);
  }
  return eventPayload_(input.client_id, input.event_name, params);
}

function appendEventSpecificGa4Params_(params, input) {
  if (input.event_name === EVENTS.VERSION_CHANGED) {
    params.previous_apl_version = input.previous_apl_version;
  }
  if ([EVENTS.BUILD_COMPLETED, EVENTS.BUILD_FAILED].includes(input.event_name)) {
    copyFields_(params, input, [
      "build_duration_ms",
      "component_count",
      "library_count",
      "category_count",
      "pose_count",
      "humanoid",
      "audio_enabled",
      "locomotion_enabled",
      "fx_enabled",
      "cache_enabled",
      "auto_reset_enabled",
    ]);
  }
  if (input.event_name === EVENTS.BUILD_FAILED) {
    copyFields_(params, input, ["build_stage", "exception_type"]);
  }
}

function legacyGa4Payload_(settings) {
  return eventPayload_(settings.legacyClientId, EVENTS.SESSION_STARTED, {
    apl_version: DEFAULTS.LEGACY_APL_VERSION,
    engagement_time_msec: 1,
  });
}

function eventPayload_(clientId, eventName, params) {
  return {
    client_id: clientId,
    consent: {
      ad_user_data: "DENIED",
      ad_personalization: "DENIED",
    },
    events: [{
      name: eventName,
      params: params,
    }],
  };
}

function sendGa4_(payload, settings) {
  if (!settings.measurementId || !settings.apiSecret) {
    return failure_("not_configured");
  }

  if (settings.debugMode) {
    payload.validation_behavior = "ENFORCE_RECOMMENDATIONS";
  }
  const response = UrlFetchApp.fetch(ga4Url_(settings), {
    method: "post",
    contentType: "application/json",
    payload: JSON.stringify(payload),
    muteHttpExceptions: true,
  });
  if (!isSuccessfulResponse_(response)) {
    return failure_("upstream_error");
  }
  if (settings.debugMode && hasGa4ValidationErrors_(response)) {
    return failure_("validation_error");
  }
  return success_();
}

function ga4Url_(settings) {
  const baseUrl = settings.debugMode
    ? "https://www.google-analytics.com/debug/mp/collect"
    : "https://www.google-analytics.com/mp/collect";
  return baseUrl
    + "?measurement_id=" + encodeURIComponent(settings.measurementId)
    + "&api_secret=" + encodeURIComponent(settings.apiSecret);
}

function hasGa4ValidationErrors_(response) {
  const result = JSON.parse(response.getContentText() || "{}");
  return Array.isArray(result.validationMessages)
    && result.validationMessages.length > 0;
}

function sendErrorReport_(report, settings) {
  if (!settings.discordBotToken
      || !matches_(settings.discordChannelId, PATTERNS.DISCORD_CHANNEL_ID)) {
    return failure_("discord_not_configured");
  }

  const safeReport = sanitizedErrorReport_(report);
  const fileName = "apl-error-" + safeReport.report_id + ".json";
  const message = {
    content: [
      "AvatarPoseLibrary error report",
      "APL: " + safeReport.apl_version,
      "Stage: " + safeReport.build_stage,
      "Exception: " + safeReport.exception_type,
    ].join("\n"),
    allowed_mentions: { parse: [] },
    attachments: [{ id: 0, filename: fileName }],
  };
  const response = UrlFetchApp.fetch(
    discordMessageUrl_(settings.discordChannelId),
    {
      method: "post",
      headers: {
        Authorization: "Bot " + settings.discordBotToken,
        "User-Agent": "DiscordBot (https://github.com/HhotateA/AvatarPoseLibrary, 1.0)",
      },
      payload: {
        payload_json: JSON.stringify(message),
        "files[0]": Utilities.newBlob(
          JSON.stringify(safeReport, null, 2),
          "application/json",
          fileName
        ),
      },
      muteHttpExceptions: true,
    }
  );
  return isSuccessfulResponse_(response)
    ? success_()
    : failure_("discord_upstream_error");
}

function discordMessageUrl_(channelId) {
  return "https://discord.com/api/v10/channels/"
    + encodeURIComponent(channelId)
    + "/messages";
}
function sanitizedErrorReport_(report) {
  const safe = JSON.parse(JSON.stringify(report));
  delete safe.request_type;
  safe.error_text = redactLocalPaths_(safe.error_text);
  return safe;
}

function redactLocalPaths_(value) {
  return String(value || "")
    .replace(/[A-Za-z]:\\[^\r\n]*/g, "[local-path]")
    .replace(/\/(?:Users|home)\/[^/\r\n]+\/[^\r\n]*/g, "[local-path]");
}

function isSuccessfulResponse_(response) {
  const code = response.getResponseCode();
  return code >= 200 && code < 300;
}

function copyFields_(target, source, fields) {
  fields.forEach((field) => {
    target[field] = source[field];
  });
}

function hasExactFields_(input, fields) {
  return isObject_(input)
    && hasRequiredAndAllowedFields_(input, fields, fields);
}

function hasRequiredAndAllowedFields_(input, requiredFields, allowedFields) {
  return isObject_(input)
    && hasFields_(input, requiredFields)
    && Object.keys(input).every((key) => allowedFields.includes(key));
}

function hasFields_(input, fields) {
  return fields.every((field) => hasOwn_(input, field));
}

function hasOwn_(input, field) {
  return Object.prototype.hasOwnProperty.call(input, field);
}

function isObject_(value) {
  return value !== null && !Array.isArray(value) && typeof value === "object";
}

function isIsoUtc_(value) {
  return matches_(value, PATTERNS.ISO_UTC) && !Number.isNaN(Date.parse(value));
}

function isVersion_(value) {
  return matches_(value, PATTERNS.VERSION);
}

function optionalVersion_(value) {
  return value === "" || isVersion_(value);
}

function matches_(value, pattern) {
  return typeof value === "string" && pattern.test(value);
}

function stringInRange_(value, minimum, maximum) {
  return typeof value === "string"
    && value.length >= minimum
    && value.length <= maximum;
}

function integerInRange_(value, minimum, maximum) {
  return Number.isSafeInteger(value) && value >= minimum && value <= maximum;
}

function binary_(value) {
  return value === 0 || value === 1;
}

function positiveInteger_(value, fallback) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : fallback;
}

function success_() {
  return { ok: true };
}

function failure_(error) {
  return { ok: false, error: error };
}

function jsonResponse_(value) {
  return ContentService
    .createTextOutput(JSON.stringify(value))
    .setMimeType(ContentService.MimeType.JSON);
}
