import { defineConfig, loadEnv, type Connect, type Plugin } from 'vite';
import vue from '@vitejs/plugin-vue';
import { fileURLToPath, URL } from 'node:url';

const VIETTEL_TTS_ENDPOINT = 'https://viettelai.vn/tts/speech_synthesis';

const devProxy = {
  '/api': {
    target: 'http://localhost:5000',
    changeOrigin: true,
    secure: false
  }
};

function readRequestBody(req: Connect.IncomingMessage, maxBytes = 16 * 1024): Promise<string> {
  return new Promise((resolve, reject) => {
    const chunks: Buffer[] = [];
    let totalBytes = 0;
    req.on('data', (chunk: Buffer | string) => {
      const buf = typeof chunk === 'string' ? Buffer.from(chunk, 'utf8') : chunk;
      totalBytes += buf.length;
      if (totalBytes > maxBytes) {
        reject(new Error('Payload too large'));
        return;
      }
      chunks.push(buf);
    });
    req.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')));
    req.on('error', reject);
  });
}

function sendJsonError(res: { statusCode: number; setHeader: (k: string, v: string) => void; end: (body?: string | Buffer) => void }, status: number, body: Buffer | string) {
  res.statusCode = status;
  res.setHeader('Content-Type', 'application/json; charset=utf-8');
  res.setHeader('Cache-Control', 'no-store');
  res.end(body);
}

function attachViettelTtsMiddleware(middlewares: Connect.Server, env: Record<string, string>) {
  middlewares.use('/tts-viettel', async (req, res, next) => {
    if (req.method !== 'POST') {
      next();
      return;
    }

    let payload: { text?: unknown; voice?: unknown; speed?: unknown };
    try {
      const rawBody = await readRequestBody(req);
      payload = JSON.parse(rawBody || '{}') as { text?: unknown; voice?: unknown; speed?: unknown };
    } catch {
      sendJsonError(res, 400, JSON.stringify({ code: 400, vi_message: 'Dữ liệu yêu cầu không hợp lệ.' }));
      return;
    }

    const text = typeof payload.text === 'string' ? payload.text.trim() : '';
    if (!text || text.length > 500) {
      sendJsonError(res, 400, JSON.stringify({ code: 400, vi_message: 'Văn bản đọc phải từ 1 đến 500 ký tự.' }));
      return;
    }

    const token = (env.VIETTEL_TTS_TOKEN ?? process.env.VIETTEL_TTS_TOKEN ?? '').trim();
    if (!token) {
      sendJsonError(res, 503, JSON.stringify({ code: 503, vi_message: 'Chưa cấu hình VIETTEL_TTS_TOKEN.' }));
      return;
    }

    const defaultVoice = (env.VIETTEL_TTS_VOICE ?? process.env.VIETTEL_TTS_VOICE ?? 'hn-quynhanh').trim() || 'hn-quynhanh';
    const voice = typeof payload.voice === 'string' && /^[a-z0-9-]+$/i.test(payload.voice.trim())
      ? payload.voice.trim()
      : defaultVoice;

    const envSpeed = Number(env.VIETTEL_TTS_SPEED ?? process.env.VIETTEL_TTS_SPEED ?? '0.95');
    const requestedSpeed = typeof payload.speed === 'number' ? payload.speed : envSpeed;
    const speed = Number.isFinite(requestedSpeed) && requestedSpeed >= 0.8 && requestedSpeed <= 1.2
      ? requestedSpeed
      : 0.95;

    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 8000);

    try {
      const upstream = await fetch(VIETTEL_TTS_ENDPOINT, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          accept: '*/*'
        },
        body: JSON.stringify({
          text,
          voice,
          speed,
          tts_return_option: 3,
          token,
          without_filter: false
        }),
        signal: controller.signal
      });
      clearTimeout(timeoutId);

      const audioBytes = Buffer.from(await upstream.arrayBuffer());
      const contentType = (upstream.headers.get('content-type') || '').toLowerCase();
      const headText = audioBytes.subarray(0, 16).toString('utf8').trimStart();
      const looksLikeJson = headText.startsWith('{') || contentType.includes('json');

      if (upstream.ok && !looksLikeJson && audioBytes.length > 256) {
        res.statusCode = 200;
        res.setHeader('Content-Type', contentType.includes('audio') ? contentType : 'audio/mpeg');
        res.setHeader('Cache-Control', 'no-store');
        res.end(audioBytes);
        return;
      }

      let errorStatus = upstream.status === 401 || upstream.status === 403 ? upstream.status : 502;
      if (looksLikeJson) {
        try {
          const parsedError = JSON.parse(audioBytes.toString('utf8')) as { code?: unknown };
          if (parsedError.code === 401 || parsedError.code === 403 || parsedError.code === 400) {
            errorStatus = parsedError.code;
          }
        } catch {}
      }

      sendJsonError(
        res,
        errorStatus,
        looksLikeJson
          ? audioBytes
          : JSON.stringify({ code: errorStatus, vi_message: 'Dịch vụ Viettel AI TTS không trả về âm thanh hợp lệ.' })
      );
    } catch {
      clearTimeout(timeoutId);
      sendJsonError(
        res,
        502,
        JSON.stringify({ code: 502, vi_message: 'Không thể kết nối dịch vụ Viettel AI TTS.' })
      );
    }
  });
}

function viettelTtsProxyPlugin(env: Record<string, string>): Plugin {
  return {
    name: 'viettel-tts-proxy',
    configureServer(server) {
      attachViettelTtsMiddleware(server.middlewares, env);
    },
    configurePreviewServer(server) {
      attachViettelTtsMiddleware(server.middlewares, env);
    }
  };
}

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  return {
    plugins: [vue(), viettelTtsProxyPlugin(env)],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url))
      }
    },
    server: {
      port: 5173,
      proxy: devProxy
    },
    preview: {
      proxy: devProxy
    }
  };
});
