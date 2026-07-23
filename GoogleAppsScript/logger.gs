const EVENTS = Object.freeze({
  SESSION_STARTED: "apl_editor_session_started",
  VERSION_CHANGED: "apl_version_changed",
  BUILD_COMPLETED: "apl_build_completed",
  BUILD_FAILED: "apl_build_failed",
  LEGACY_SESSION_STARTED: "apl_legacy_editor_session_started",
});

const ALLOWED_POST_EVENTS = Object.freeze([
  EVENTS.VERSION_CHANGED,
  EVENTS.BUILD_COMPLETED,
  EVENTS.BUILD_FAILED,
]);

const MINIMAL_FIELDS = Object.freeze([
  "schema_version",
  "telemetry_mode",
  "event_name",
  "client_id",
  "apl_version",
]);

const ENVIRONMENT_FIELDS = Object.freeze([
  "event_id",
  "unity_version",
  "vrcsdk_version",
  "ndmf_version",
  "session_id",
  "engagement_time_msec",
]);

const DETAILED_SESSION_FIELDS = Object.freeze(
  MINIMAL_FIELDS.concat(ENVIRONMENT_FIELDS)
);

const BUILD_FIELDS = Object.freeze([
  "previous_apl_version",
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
  "build_stage",
  "exception_type",
]);

const DETAILED_FIELDS = Object.freeze(
  DETAILED_SESSION_FIELDS.concat(BUILD_FIELDS)
);

const GET_INTEGER_FIELDS = Object.freeze([
  "schema_version",
  "session_id",
  "engagement_time_msec",
]);

function doGet(e) {
  const version = latestVersion_();
  if (version === null) {
    return jsonResponse_({ error: "version_sheet_not_found" });
  }

  const settings = settings_();
  let result;
  try {
    const input = telemetryInputFromGet_(e);
    if (input === null) {
      result = sendGa4_(legacyGa4Payload_(settings), settings);
    } else if (!validateSession_(input, settings.schemaVersion)) {
      result = failure_("invalid_request");
    } else {
      result = sendGa4_(ga4Payload_(input), settings);
    }
  } catch (_) {
    // A telemetry failure must never break the version endpoint.
    result = failure_("internal_error");
  }

  const output = {
    version: version,
    telemetry_ok: result.ok,
  };
  if (!result.ok) {
    output.telemetry_error = result.error;
  }
  return jsonResponse_(output);
}

function doPost(e) {
  try {
    const settings = settings_();
    const input = telemetryInputFromPost_(e, settings.maximumRequestBytes);
    if (input === null || !validatePostEvent_(input, settings.schemaVersion)) {
      return invalidRequest_();
    }

    return jsonResponse_(sendGa4_(ga4Payload_(input), settings));
  } catch (_) {
    return jsonResponse_(failure_("internal_error"));
  }
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
  return {
    measurementId: properties.getProperty("GA4_MEASUREMENT_ID"),
    apiSecret: properties.getProperty("GA4_API_SECRET"),
    schemaVersion: positiveInteger_(
      properties.getProperty("SCHEMA_VERSION"),
      1
    ),
    maximumRequestBytes: positiveInteger_(
      properties.getProperty("MAX_REQUEST_BYTES"),
      8192
    ),
    debugMode:
      String(properties.getProperty("GA4_DEBUG_MODE")).toLowerCase() === "true",
    legacyClientId:
      properties.getProperty("GA4_LEGACY_CLIENT_ID")
      || "00000000000000000000000000000000",
  };
}

function telemetryInputFromGet_(e) {
  const parameters = e && isObject_(e.parameter) ? e.parameter : {};
  if (!hasFields_(parameters, MINIMAL_FIELDS)) {
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

function telemetryInputFromPost_(e, maximumBytes) {
  if (!e || !e.postData
      || typeof e.postData.contents !== "string"
      || !String(e.postData.type || "").toLowerCase().startsWith("application/json")
      || Number(e.contentLength || e.postData.length || 0) > maximumBytes) {
    return null;
  }

  try {
    return JSON.parse(e.postData.contents);
  } catch (_) {
    return null;
  }
}

function validateSession_(input, expectedSchema) {
  if (!validateCommon_(input, expectedSchema, [EVENTS.SESSION_STARTED])) {
    return false;
  }

  const fields = input.telemetry_mode === "detailed"
    ? DETAILED_SESSION_FIELDS
    : MINIMAL_FIELDS;
  return hasExactFields_(input, fields)
    && (input.telemetry_mode === "minimal" || validateEnvironment_(input));
}

function validatePostEvent_(input, expectedSchema) {
  if (!validateCommon_(input, expectedSchema, ALLOWED_POST_EVENTS)) {
    return false;
  }

  const fields = input.telemetry_mode === "detailed"
    ? DETAILED_FIELDS
    : MINIMAL_FIELDS;
  if (!hasExactFields_(input, fields)) {
    return false;
  }

  return input.telemetry_mode === "minimal"
    || (validateEnvironment_(input) && validateBuildFields_(input));
}

function validateCommon_(input, expectedSchema, allowedEvents) {
  return isObject_(input)
    && input.schema_version === expectedSchema
    && (input.telemetry_mode === "minimal"
        || input.telemetry_mode === "detailed")
    && allowedEvents.includes(input.event_name)
    && matches_(input.client_id, /^[a-f0-9]{32}$/)
    && isVersion_(input.apl_version);
}

function validateEnvironment_(input) {
  return matches_(input.event_id, /^[a-f0-9]{32}$/)
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
    && matches_(input.build_stage, /^[a-z0-9_]{0,40}$/)
    && matches_(input.exception_type, /^[A-Za-z0-9_.+`]{0,100}$/);
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
    copyFields_(params, input, [
      "event_id",
      "unity_version",
      "vrcsdk_version",
      "ndmf_version",
      "session_id",
    ]);

    if (input.event_name === EVENTS.VERSION_CHANGED) {
      params.previous_apl_version = input.previous_apl_version;
    }
    if (input.event_name === EVENTS.BUILD_COMPLETED
        || input.event_name === EVENTS.BUILD_FAILED) {
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

  return eventPayload_(input.client_id, input.event_name, params);
}

function legacyGa4Payload_(settings) {
  return eventPayload_(settings.legacyClientId, EVENTS.SESSION_STARTED, {
    apl_version: "1.2.35",
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

  const baseUrl = settings.debugMode
    ? "https://www.google-analytics.com/debug/mp/collect"
    : "https://www.google-analytics.com/mp/collect";
  const url = baseUrl
    + "?measurement_id=" + encodeURIComponent(settings.measurementId)
    + "&api_secret=" + encodeURIComponent(settings.apiSecret);
  if (settings.debugMode) {
    payload.validation_behavior = "ENFORCE_RECOMMENDATIONS";
  }

  const response = UrlFetchApp.fetch(url, {
    method: "post",
    contentType: "application/json",
    payload: JSON.stringify(payload),
    muteHttpExceptions: true,
  });
  const responseCode = response.getResponseCode();
  if (responseCode < 200 || responseCode >= 300) {
    return failure_("upstream_error");
  }

  if (settings.debugMode) {
    const result = JSON.parse(response.getContentText() || "{}");
    if (Array.isArray(result.validationMessages)
        && result.validationMessages.length > 0) {
      return failure_("validation_error");
    }
  }
  return { ok: true };
}

function copyFields_(target, source, fields) {
  fields.forEach((field) => {
    target[field] = source[field];
  });
}

function hasExactFields_(input, fields) {
  return hasFields_(input, fields)
    && Object.keys(input).every((key) => fields.includes(key));
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

function isVersion_(value) {
  return matches_(value, /^[0-9A-Za-z][0-9A-Za-z._+\-]{0,63}$/);
}

function optionalVersion_(value) {
  return value === "" || isVersion_(value);
}

function matches_(value, pattern) {
  return typeof value === "string" && pattern.test(value);
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

function failure_(error) {
  return { ok: false, error: error };
}

function invalidRequest_() {
  return jsonResponse_(failure_("invalid_request"));
}

function jsonResponse_(value) {
  return ContentService
    .createTextOutput(JSON.stringify(value))
    .setMimeType(ContentService.MimeType.JSON);
}