using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ActionConstraints;

namespace HRMBT.Web.Infrastructure;

/// <summary>
/// Old conventional URLs (/Employee/Details/5, /LMS/LeaveQuota, /Setup/EditUser/3, ...) now redirect (301) to the
/// attribute-route URL of the same action, so bookmarks and printed links keep working. The table is built from the
/// actions themselves (controller + action name -> route template), so it never needs hand maintenance.
/// </summary>
public static class LegacyRouteRedirects
{
    public static IApplicationBuilder UseLegacyRouteRedirects(this IApplicationBuilder app)
    {
        Dictionary<string, string>? map = null;
        var gate = new object();

        return app.Use(async (context, next) =>
        {
            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            {
                await next();
                return;
            }

            var segments = context.Request.Path.Value?.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            if (segments.Length is < 1 or > 3)
            {
                await next();
                return;
            }

            if (map is null)
            {
                lock (gate)
                {
                    map ??= Build(context.RequestServices.GetRequiredService<IActionDescriptorCollectionProvider>());
                }
            }

            // /Controller -> Index, /Controller/Action, /Controller/Action/id
            var key = (segments[0] + "/" + (segments.Length > 1 ? segments[1] : "Index")).ToLowerInvariant();
            if (!map.TryGetValue(key, out var template))
            {
                await next();
                return;
            }

            // /Controller/Action/<something> is an old URL only when the action takes an id; otherwise it is a new URL (/setup/users/new).
            if (segments.Length > 2 && !template.Contains("{id}"))
            {
                await next();
                return;
            }

            var id = segments.Length > 2 ? Uri.EscapeDataString(segments[2]) : context.Request.Query["id"].ToString();
            var kind = context.Request.Query["kind"].ToString();
            var query = new List<string>();
            foreach (var q in context.Request.Query)
            {
                if ((q.Key.Equals("id", StringComparison.OrdinalIgnoreCase) && template.Contains("{id}")) ||
                    (q.Key.Equals("kind", StringComparison.OrdinalIgnoreCase) && template.Contains("{kind")))
                    continue;
                foreach (var v in q.Value) query.Add($"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(v ?? "")}");
            }

            var path = template.Replace("{id}", id).Replace("{kind?}", kind).Replace("{kind}", kind);
            if (path.Contains('{') || path.Contains("//"))
            {
                await next();
                return;
            }
            path = "/" + path.Trim('/');
            if (string.Equals(path, context.Request.Path.Value?.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) ||
                (path == "/" && context.Request.Path.Value == "/"))
            {
                await next();
                return;
            }

            context.Response.Redirect(context.Request.PathBase + path + (query.Count > 0 ? "?" + string.Join("&", query) : ""), permanent: true);
        });
    }

    private static Dictionary<string, string> Build(IActionDescriptorCollectionProvider provider)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in provider.ActionDescriptors.Items.OfType<ControllerActionDescriptor>())
        {
            var template = d.AttributeRouteInfo?.Template;
            if (template is null) continue;
            var isGet = d.ActionConstraints?.OfType<HttpMethodActionConstraint>().Any(c => c.HttpMethods.Contains("GET")) ?? true;
            if (!isGet) continue;
            var key = (d.ControllerName + "/" + d.ActionName).ToLowerInvariant();
            // template as seen by the router is already the full path (controller prefix included)
            result.TryAdd(key, template);
        }
        // The old default route opened the login page at "/" and "/Home".
        result["home/login"] = "login";
        return result;
    }
}
