import os
import json
from collections import Counter, defaultdict
from pathlib import Path
import matplotlib.pyplot as plt

# =====================================================
# CONFIG
# =====================================================

BASE_DIR = Path(__file__).resolve().parent
DEFAULT_LABELED_DIR = BASE_DIR.parent / "synthetic_invoices" / "output" / "labeled_tokens"

LABELED_DIR = Path(os.getenv("LABELED_DIR", DEFAULT_LABELED_DIR)).expanduser().resolve()

COVERAGE_WARNING_THRESHOLD = 80  # %

# =====================================================
# INIT
# =====================================================

field_token_counter = Counter()
field_invoice_presence = defaultdict(int)

total_invoices = 0
total_tokens_all = 0
total_labeled_tokens_all = 0

# =====================================================
# READ FILES
# =====================================================

for filename in os.listdir(LABELED_DIR):
    if not filename.endswith(".json"):
        continue

    total_invoices += 1
    filepath = os.path.join(LABELED_DIR, filename)

    with open(filepath, "r", encoding="utf-8") as f:
        data = json.load(f)

    tokens = data.get("tokens", [])
    total_tokens_all += len(tokens)

    invoice_fields = set()

    for token in tokens:
        label = token.get("label", "O")
        field = token.get("field", None)

        if label != "O" and field is not None:
            field_token_counter[field] += 1
            invoice_fields.add(field)
            total_labeled_tokens_all += 1

    for f in invoice_fields:
        field_invoice_presence[f] += 1


# =====================================================
# PRINT GLOBAL STATS
# =====================================================

print("=" * 70)
print("📊 GLOBAL DATASET STATISTICS")
print("=" * 70)
print(f"Total invoices:        {total_invoices}")
print(f"Total tokens:          {total_tokens_all}")
print(f"Total labeled tokens:  {total_labeled_tokens_all}")

coverage = total_labeled_tokens_all / total_tokens_all * 100
print(f"Overall label coverage: {coverage:.2f}%")

print("\n")

# =====================================================
# TOKEN DISTRIBUTION PER FIELD
# =====================================================

print("=" * 70)
print("📊 TOKEN DISTRIBUTION PER FIELD")
print("=" * 70)

for field, count in field_token_counter.most_common():
    print(f"{field:<30} {count}")

print("\n")

# =====================================================
# INVOICE-LEVEL FIELD COVERAGE
# =====================================================

print("=" * 70)
print("📊 INVOICE-LEVEL FIELD COVERAGE")
print("=" * 70)

field_coverage_percent = {}

for field, count in sorted(field_invoice_presence.items()):
    percent = count / total_invoices * 100
    field_coverage_percent[field] = percent

    warning = ""
    if percent < COVERAGE_WARNING_THRESHOLD:
        warning = " ⚠️ LOW"

    print(f"{field:<30} {count}/{total_invoices} ({percent:.2f}%) {warning}")

print("\n")

# =====================================================
# PLOT 1: TOKEN COUNT
# =====================================================

fields = list(field_token_counter.keys())
counts = list(field_token_counter.values())

plt.figure()
plt.bar(fields, counts)
plt.xticks(rotation=90)
plt.title("Token Count per Field")
plt.tight_layout()
plt.show()

# =====================================================
# PLOT 2: INVOICE COVERAGE %
# =====================================================

fields2 = list(field_coverage_percent.keys())
coverage2 = [field_coverage_percent[f] for f in fields2]

plt.figure()
plt.bar(fields2, coverage2)
plt.xticks(rotation=90)
plt.title("Invoice-Level Field Coverage (%)")
plt.tight_layout()
plt.show()

print("Done.")
