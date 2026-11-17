import tempfile
import unittest
import wave
from pathlib import Path
from unittest.mock import patch

import numpy as np

import bob_generate_audio as audio


class AudioExportTests(unittest.TestCase):
    def test_export_keeps_format_duration_and_peak_with_silent_boundaries(self):
        for frames in (2, audio.SR // 10):
            with self.subTest(frames=frames), tempfile.TemporaryDirectory() as folder:
                source = np.ones((frames, 2))
                with patch.object(audio, "OUT_DIR", folder):
                    audio.save_wav("cue.wav", source)
                with wave.open(str(Path(folder) / "cue.wav"), "rb") as wav:
                    self.assertEqual((wav.getnchannels(), wav.getsampwidth(), wav.getframerate(), wav.getnframes()),
                                     (2, 2, audio.SR, frames))
                    samples = np.frombuffer(wav.readframes(frames), dtype="<i2").reshape(-1, 2)
                np.testing.assert_array_equal(samples[[0, -1]], 0)
                np.testing.assert_array_equal(source, 1)
                self.assertLessEqual(np.abs(samples).max(), round(32767 * 0.92))
                if frames > 2:
                    self.assertAlmostEqual(samples[frames // 2, 0] / 32767, 0.92, places=4)


if __name__ == "__main__":
    unittest.main()
