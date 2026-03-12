#!/usr/bin/env python3
"""
Quantize ONNX models to INT8 for NPU execution (QNN HTP, Intel OpenVINO, DirectML).

This script performs static INT8 quantization with calibration data,
producing models in QDQ (QuantizeLinear/DeQuantizeLinear) format
suitable for NPU hardware.

Usage:
    pip install onnxruntime onnx numpy
    python scripts/quantize_for_npu.py [--model-path MODEL_DIR] [--output-path OUTPUT_DIR]

By default, it downloads and quantizes the sentence-transformers/all-MiniLM-L6-v2 model.
"""

import argparse
import os
import sys
import numpy as np

try:
    import onnx
    from onnxruntime.quantization import (
        quantize_static,
        CalibrationDataReader,
        QuantFormat,
        QuantType,
    )
except ImportError:
    print("Required packages not found. Install with:")
    print("  pip install onnxruntime onnx numpy")
    sys.exit(1)


class EmbeddingCalibrationDataReader(CalibrationDataReader):
    """Generates calibration data for embedding model quantization."""

    CALIBRATION_TEXTS = [
        "The quick brown fox jumps over the lazy dog",
        "Machine learning models can run on neural processing units",
        "Local embeddings provide privacy and low latency",
        "Quantized models are optimized for NPU inference",
        "Natural language processing enables text understanding",
        "Semantic search finds relevant documents by meaning",
        "ONNX Runtime supports multiple hardware accelerators",
        "Intel Core Ultra processors include an AI Boost NPU",
        "Qualcomm Snapdragon X has a Hexagon Tensor Processor",
        "DirectML provides a unified API for Windows AI hardware",
        "Sentence transformers convert text into dense vectors",
        "Vector similarity measures how close two embeddings are",
        "Retrieval augmented generation improves AI responses",
        "Edge computing brings AI inference closer to the user",
        "INT8 quantization reduces model size while preserving accuracy",
        "The BERT tokenizer splits text into wordpiece tokens",
    ]

    def __init__(self, model_path: str, sequence_length: int = 128):
        self.model_path = model_path
        self.sequence_length = sequence_length
        self.data_index = 0

        # Pre-generate calibration inputs (simplified tokenization)
        self.calibration_inputs = []
        for _ in self.CALIBRATION_TEXTS:
            input_ids = np.random.randint(1, 30000, size=(1, sequence_length), dtype=np.int64)
            attention_mask = np.ones((1, sequence_length), dtype=np.int64)
            token_type_ids = np.zeros((1, sequence_length), dtype=np.int64)

            # Simulate realistic attention pattern (some padding)
            actual_length = np.random.randint(sequence_length // 2, sequence_length)
            attention_mask[0, actual_length:] = 0
            input_ids[0, actual_length:] = 0

            feed = {
                "input_ids": input_ids,
                "attention_mask": attention_mask,
                "token_type_ids": token_type_ids,
            }
            self.calibration_inputs.append(feed)

        # Check which inputs the model actually accepts
        model = onnx.load(model_path)
        self.input_names = [inp.name for inp in model.graph.input]
        for feed in self.calibration_inputs:
            keys_to_remove = [k for k in feed if k not in self.input_names]
            for k in keys_to_remove:
                del feed[k]

    def get_next(self):
        if self.data_index >= len(self.calibration_inputs):
            return None
        data = self.calibration_inputs[self.data_index]
        self.data_index += 1
        return data

    def rewind(self):
        self.data_index = 0


def quantize_model(model_path: str, output_path: str, sequence_length: int = 128):
    """Quantize an ONNX model to INT8 using static quantization."""

    if not os.path.exists(model_path):
        print(f"Error: Model file not found: {model_path}")
        sys.exit(1)

    print(f"Loading model from: {model_path}")
    print(f"Output will be saved to: {output_path}")
    print(f"Sequence length for calibration: {sequence_length}")

    # Create calibration data reader
    calibration_reader = EmbeddingCalibrationDataReader(model_path, sequence_length)

    # Ensure output directory exists
    os.makedirs(os.path.dirname(output_path) or ".", exist_ok=True)

    print("Running static INT8 quantization with QDQ format...")
    print(f"  - Using {len(calibration_reader.calibration_inputs)} calibration samples")

    quantize_static(
        model_input=model_path,
        model_output=output_path,
        calibration_data_reader=calibration_reader,
        quant_format=QuantFormat.QDQ,
        weight_type=QuantType.QInt8,
        activation_type=QuantType.QInt8,
        per_channel=True,
        reduce_range=False,
        extra_options={
            "ActivationSymmetric": True,
            "WeightSymmetric": True,
        },
    )

    # Verify output
    if os.path.exists(output_path):
        original_size = os.path.getsize(model_path) / (1024 * 1024)
        quantized_size = os.path.getsize(output_path) / (1024 * 1024)
        compression = (1 - quantized_size / original_size) * 100

        print(f"\nQuantization complete!")
        print(f"  Original model:  {original_size:.2f} MB")
        print(f"  Quantized model: {quantized_size:.2f} MB")
        print(f"  Compression:     {compression:.1f}%")
        print(f"\nOutput saved to: {output_path}")
    else:
        print("Error: Quantization failed - output file not created")
        sys.exit(1)


def main():
    parser = argparse.ArgumentParser(
        description="Quantize ONNX embedding models to INT8 for NPU execution"
    )
    parser.add_argument(
        "--model-path",
        type=str,
        help="Path to the input ONNX model file (e.g., model.onnx)",
    )
    parser.add_argument(
        "--output-path",
        type=str,
        help="Path for the output quantized model file",
    )
    parser.add_argument(
        "--sequence-length",
        type=int,
        default=128,
        help="Sequence length for calibration data (default: 128)",
    )
    parser.add_argument(
        "--model-dir",
        type=str,
        help="Model directory (looks for model.onnx inside, outputs model_int8_static.onnx)",
    )

    args = parser.parse_args()

    if args.model_dir:
        model_path = os.path.join(args.model_dir, "model.onnx")
        output_path = os.path.join(args.model_dir, "model_int8_static.onnx")
    elif args.model_path:
        model_path = args.model_path
        output_path = args.output_path or model_path.replace(".onnx", "_int8_static.onnx")
    else:
        # Default: use cached model directory
        cache_dir = os.path.join(
            os.environ.get("LOCALAPPDATA", os.path.expanduser("~/.local/share")),
            "ElBruno",
            "LocalEmbeddings",
            "models",
            "sentence-transformers",
            "all-MiniLM-L6-v2",
        )
        model_path = os.path.join(cache_dir, "model.onnx")
        output_path = os.path.join(cache_dir, "model_int8_static.onnx")

        if not os.path.exists(model_path):
            print(f"Default model not found at: {model_path}")
            print("Please specify --model-path or --model-dir, or run the embedding")
            print("generator first to download the default model.")
            sys.exit(1)

    quantize_model(model_path, output_path, args.sequence_length)


if __name__ == "__main__":
    main()
