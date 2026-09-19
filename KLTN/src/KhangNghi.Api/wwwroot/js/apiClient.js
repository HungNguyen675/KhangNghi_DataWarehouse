/* ==========================================================================
   KHANG NGHI BUSINESS INTELLIGENCE - REAL REST API CLIENT
   HTTP Fetch Wrapper connecting to ASP.NET Core .NET 9 Web API
   ========================================================================== */

window.KhangNghiApiClient = (function () {
  // Configurable Base URL (supports local dev & production relative paths)
  let BASE_URL = window.location.origin.includes("localhost:5000") || window.location.origin.includes("localhost:8080") || window.location.origin.includes("127.0.0.1")
    ? "http://localhost:5000/api/v1"
    : "/api/v1";

  function setBaseUrl(url) {
    if (!url) return;
    BASE_URL = url.replace(/\/$/, "");
  }

  function getBaseUrl() {
    return BASE_URL;
  }

  function getToken() {
    return localStorage.getItem("kn_jwt_token") || sessionStorage.getItem("kn_jwt_token");
  }

  function saveToken(token, remember = false) {
    if (remember) {
      localStorage.setItem("kn_jwt_token", token);
      sessionStorage.removeItem("kn_jwt_token");
    } else {
      sessionStorage.setItem("kn_jwt_token", token);
      localStorage.removeItem("kn_jwt_token");
    }
  }

  function clearToken() {
    localStorage.removeItem("kn_jwt_token");
    sessionStorage.removeItem("kn_jwt_token");
  }

  async function request(endpoint, options = {}) {
    const url = endpoint.startsWith("http") ? endpoint : `${BASE_URL}${endpoint.startsWith("/") ? "" : "/"}${endpoint}`;
    
    const headers = {
      "Content-Type": "application/json",
      "Accept": "application/json",
      ...(options.headers || {})
    };

    const token = getToken();
    if (token) {
      headers["Authorization"] = `Bearer ${token}`;
    }

    const config = {
      method: options.method || "GET",
      headers,
      ...options
    };

    if (options.body && typeof options.body === "object") {
      config.body = JSON.stringify(options.body);
    }

    const controller = new AbortController();
    const timeoutMs = options.timeout || 8000;
    const timeoutId = setTimeout(() => controller.abort(), timeoutMs);
    config.signal = controller.signal;

    try {
      const response = await fetch(url, config);
      clearTimeout(timeoutId);

      if (response.status === 401) {
        console.warn("[ApiClient] 401 Unauthorized - Invalid or expired JWT token");
        clearToken();
        if (window.location.hash !== "#/login") {
          window.location.hash = "#/login";
        }
        return { success: false, status: 401, error: "Unauthorized (401)" };
      }

      if (response.status === 403) {
        console.warn("[ApiClient] 403 Forbidden - Insufficient permissions for endpoint:", url);
        return { success: false, status: 403, error: "Forbidden (403)" };
      }

      if (!response.ok) {
        const errorText = await response.text().catch(() => "");
        console.error(`[ApiClient] HTTP ${response.status} Error:`, errorText);
        return {
          success: false,
          status: response.status,
          error: `HTTP Error ${response.status}: ${errorText || response.statusText}`
        };
      }

      const data = await response.json();
      return { success: true, status: response.status, data };
    } catch (err) {
      clearTimeout(timeoutId);
      if (err.name === "AbortError") {
        console.error("[ApiClient] Request Timeout (8s exceeded):", url);
        return { success: false, status: 408, error: "Request Timeout (408)" };
      }
      console.warn("[ApiClient] Network error connecting to Backend API:", url, err.message);
      return { success: false, status: 0, error: "Cannot Connect API: " + err.message };
    }
  }

  function get(endpoint, params = {}) {
    let queryString = "";
    if (params && Object.keys(params).length > 0) {
      const cleanParams = {};
      Object.keys(params).forEach(key => {
        const val = params[key];
        if (val !== null && val !== undefined && val !== "" && val !== "ALL" && val !== "All Regions" && val !== "All Categories" && val !== "All Segments") {
          cleanParams[key] = val;
        }
      });
      if (Object.keys(cleanParams).length > 0) {
        queryString = "?" + new URLSearchParams(cleanParams).toString();
      }
    }
    return request(`${endpoint}${queryString}`, { method: "GET" });
  }

  function post(endpoint, body = {}) {
    return request(endpoint, { method: "POST", body });
  }

  return {
    setBaseUrl,
    getBaseUrl,
    getToken,
    saveToken,
    clearToken,
    request,
    get,
    post
  };
})();
