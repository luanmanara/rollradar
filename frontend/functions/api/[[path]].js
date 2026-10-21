// Cloudflare Pages Function: forwards /api/* to the API, so the site keeps calling /api on its own domain
// (no CORS). API_ORIGIN (and PROXY_SECRET, when the API sits behind Caddy) are set in the Pages project.
export async function onRequest({ request, env }) {
  const url = new URL(request.url);
  const upstream = new Request(new URL(url.pathname + url.search, env.API_ORIGIN), request);
  upstream.headers.set('X-Client-IP', request.headers.get('CF-Connecting-IP') ?? '');
  upstream.headers.set('X-Forwarded-For', request.headers.get('CF-Connecting-IP') ?? '');
  if (env.PROXY_SECRET) upstream.headers.set('X-Proxy-Secret', env.PROXY_SECRET);
  return fetch(upstream);
}
