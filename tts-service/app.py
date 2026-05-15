"""
MURO Podcast TTS Mikroservisi — Google Cloud TTS Chirp3-HD
En yüksek kaliteli, doğal Türkçe ses üretimi.
Endpoint: POST /synthesize { "script": "...", "voice": "tr-TR-Chirp3-HD-Achird" }
"""
import base64
import io
import os
import requests
from flask import Flask, request, send_file, jsonify

app = Flask(__name__)

# Google Cloud TTS API
GOOGLE_TTS_URL = "https://texttospeech.googleapis.com/v1/text:synthesize"
API_KEY = os.environ.get("GOOGLE_API_KEY", "")

# Türkçe Chirp3-HD sesleri — Google'ın en yeni ve en kaliteli sesleri
VOICES = {
    # Erkek sesler
    "tr-TR-Chirp3-HD-Achird":    {"gender": "MALE",   "label": "Achird (Erkek)"},
    "tr-TR-Chirp3-HD-Enceladus": {"gender": "MALE",   "label": "Enceladus (Erkek)"},
    "tr-TR-Chirp3-HD-Charon":    {"gender": "MALE",   "label": "Charon (Erkek)"},
    "tr-TR-Chirp3-HD-Fenrir":    {"gender": "MALE",   "label": "Fenrir (Erkek)"},
    "tr-TR-Chirp3-HD-Puck":      {"gender": "MALE",   "label": "Puck (Erkek)"},
    # Kadın sesler
    "tr-TR-Chirp3-HD-Achernar":  {"gender": "FEMALE", "label": "Achernar (Kadın)"},
    "tr-TR-Chirp3-HD-Aoede":     {"gender": "FEMALE", "label": "Aoede (Kadın)"},
    "tr-TR-Chirp3-HD-Kore":      {"gender": "FEMALE", "label": "Kore (Kadın)"},
    "tr-TR-Chirp3-HD-Leda":      {"gender": "FEMALE", "label": "Leda (Kadın)"},
    "tr-TR-Chirp3-HD-Zephyr":    {"gender": "FEMALE", "label": "Zephyr (Kadın)"},
}

DEFAULT_VOICE = "tr-TR-Chirp3-HD-Achird"  # Erkek, doğal


@app.route("/health", methods=["GET"])
def health():
    return jsonify({
        "status": "ok",
        "engine": "google-cloud-tts-chirp3-hd",
        "voices": [{"id": k, "label": v["label"]} for k, v in VOICES.items()],
        "apiKeyConfigured": bool(API_KEY),
    })


@app.route("/voices", methods=["GET"])
def list_voices():
    """Kullanılabilir Türkçe Chirp3-HD seslerini listele."""
    return jsonify([
        {"id": k, "label": v["label"], "gender": v["gender"]}
        for k, v in VOICES.items()
    ])


@app.route("/synthesize", methods=["POST"])
def synthesize():
    """Metni Google Cloud TTS Chirp3-HD ile sese çevirip MP3 olarak döndür."""
    if not API_KEY:
        return jsonify({"error": "GOOGLE_API_KEY ayarlanmamış"}), 500

    data = request.get_json(force=True)
    script = data.get("script", "").strip()
    voice = data.get("voice", DEFAULT_VOICE)

    if not script:
        return jsonify({"error": "Script boş olamaz."}), 400

    # Bilinen ses listesinde yoksa default kullan
    if voice not in VOICES:
        # Eski edge-tts seslerini map et
        if "Ahmet" in voice or voice == "tr-TR-AhmetNeural":
            voice = "tr-TR-Chirp3-HD-Achird"
        elif "Emel" in voice or voice == "tr-TR-EmelNeural":
            voice = "tr-TR-Chirp3-HD-Aoede"
        else:
            voice = DEFAULT_VOICE

    if len(script) > 5000:
        return _synthesize_long(script, voice)

    try:
        audio_bytes = _call_google_tts(script, voice)
        buffer = io.BytesIO(audio_bytes)
        return send_file(buffer, mimetype="audio/mpeg", as_attachment=False, download_name="podcast.mp3")
    except Exception as e:
        return jsonify({"error": f"TTS hatası: {str(e)}"}), 500


def _call_google_tts(text: str, voice: str) -> bytes:
    """Google Cloud TTS API'ye tek istek gönder."""
    body = {
        "input": {"text": text},
        "voice": {
            "languageCode": "tr-TR",
            "name": voice,
        },
        "audioConfig": {
            "audioEncoding": "MP3",
            "speakingRate": 0.95,
            "pitch": 0.0,
            "sampleRateHertz": 24000,
            "effectsProfileId": ["headphone-class-device"],
        },
    }

    resp = requests.post(f"{GOOGLE_TTS_URL}?key={API_KEY}", json=body, timeout=30)

    if resp.status_code != 200:
        error_msg = resp.json().get("error", {}).get("message", resp.text)
        raise Exception(f"Google TTS API hatası ({resp.status_code}): {error_msg}")

    return base64.b64decode(resp.json()["audioContent"])


def _synthesize_long(script: str, voice: str):
    """5000+ karakter metinleri parçalara bölerek sentezle."""
    chunks, current = [], ""
    for sentence in script.replace("!", "!|").replace("?", "?|").replace(".", ".|").split("|"):
        sentence = sentence.strip()
        if not sentence:
            continue
        if len(current) + len(sentence) + 1 > 4500:
            if current:
                chunks.append(current)
            current = sentence
        else:
            current = f"{current} {sentence}".strip()
    if current:
        chunks.append(current)

    all_audio = b""
    for chunk in chunks:
        all_audio += _call_google_tts(chunk, voice)

    buffer = io.BytesIO(all_audio)
    return send_file(buffer, mimetype="audio/mpeg", as_attachment=False, download_name="podcast.mp3")


if __name__ == "__main__":
    if not API_KEY:
        import json
        try:
            settings_path = os.path.join(os.path.dirname(__file__), "..", "src", "MURO.API", "appsettings.json")
            with open(settings_path) as f:
                settings = json.load(f)
                API_KEY = settings.get("Gemini", {}).get("ApiKey", "")
                print(f"✅ API Key appsettings.json'dan yüklendi")
        except Exception:
            pass

    if not API_KEY:
        print("⚠️  GOOGLE_API_KEY bulunamadı!")
    else:
        print(f"✅ API Key: ...{API_KEY[-8:]}")

    print("🎙️  Google Cloud TTS Chirp3-HD servisi — http://localhost:5050")
    app.run(host="0.0.0.0", port=5050, debug=False)
