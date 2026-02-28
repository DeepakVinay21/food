from fastapi import FastAPI, File, UploadFile, HTTPException
from fastapi.responses import JSONResponse
from paddleocr import PaddleOCR
import numpy as np
import cv2


app = FastAPI(title="Food OCR Service", version="1.0.0")

# English model for packaging text. Angle classifier helps rotated labels.
ocr = PaddleOCR(use_angle_cls=True, lang="en", show_log=False)


@app.get("/health")
def health():
    return {"status": "ok"}


@app.post("/ocr/extract")
async def extract(image: UploadFile = File(...)):
    try:
        payload = await image.read()
        if not payload:
            raise HTTPException(status_code=400, detail="Empty image")

        arr = np.frombuffer(payload, np.uint8)
        mat = cv2.imdecode(arr, cv2.IMREAD_COLOR)
        if mat is None:
            raise HTTPException(status_code=400, detail="Invalid image")

        result = ocr.ocr(mat, cls=True)
        lines = []
        if result and result[0]:
            for item in result[0]:
                # item => [box, (text, score)]
                text = item[1][0].strip() if item and item[1] else ""
                score = float(item[1][1]) if item and item[1] else 0.0
                if len(text) >= 2 and score >= 0.35:
                    lines.append(text)

        cleaned = "\n".join(lines).strip()
        return JSONResponse({"text": cleaned})
    except HTTPException:
        raise
    except Exception as exc:
        raise HTTPException(status_code=500, detail=f"OCR failed: {exc}")

