"""Compara docs/06-esquema-fase1.sql contra las columnas reales de la base.

El documento de diseno no se ejecuta nunca, asi que nada garantiza que siga
describiendo lo que hay. Esto lo comprueba en lugar de suponerlo.
"""
import re
import sys
import pathlib

BACK = pathlib.Path(r"C:\Users\IMANOL~1\Desktop\Maquinaria\maquinaria-backend")
SP = pathlib.Path(r"C:\Users\IMANOL~1\AppData\Local\Temp\claude"
                  r"\C--Users-ImanolV-zquezSalvado-Desktop-Maquinaria"
                  r"\c5f4ad6d-66e0-4ae6-8a07-fcf391ef1af1\scratchpad")

FASE_0 = {"__EFMigrationsHistory", "usuario", "token_acceso", "sesion_refresh",
          "permiso", "rol", "rol_permiso", "usuario_rol", "parametro", "archivo",
          "auditoria"}

NO_ES_COLUMNA = {"constraint", "primary", "unique", "check", "foreign", "exclude"}


def definiciones(cuerpo):
    """Parte el cuerpo de un CREATE TABLE por las comas de PROFUNDIDAD CERO.

    Es la unica forma correcta: una linea sola no basta porque un CONSTRAINT se
    parte en varias, y su continuacion —'REFERENCES equipo (id)'— parece una
    columna si se mira aislada. Un primer intento uso lista negra de palabras y
    acabo tapando columnas reales como equipo_id.
    """
    partes, actual, profundidad = [], [], 0
    for caracter in cuerpo:
        if caracter == "(":
            profundidad += 1
        elif caracter == ")":
            profundidad -= 1
        if caracter == "," and profundidad == 0:
            partes.append("".join(actual))
            actual = []
        else:
            actual.append(caracter)
    partes.append("".join(actual))
    return partes


# ------------------------------------------------- el documento de diseno ----
texto = (BACK / "docs/06-esquema-fase1.sql").read_text(encoding="utf-8")
diseno = {}

for bloque in re.finditer(r"CREATE TABLE (\w+) \((.*?)\n\);", texto, re.S):
    tabla, cuerpo = bloque.group(1), bloque.group(2)
    columnas = set()
    for parte in definiciones(cuerpo):
        limpio = "\n".join(
            l for l in parte.split("\n") if not l.strip().startswith("--")).strip()
        if not limpio:
            continue
        primera = limpio.split()[0].lower()
        if primera in NO_ES_COLUMNA:
            continue
        columnas.add(primera)
    diseno[tabla] = columnas

# ------------------------------------------------------- la base real -------
real = {}
for linea in (SP / "columnas-reales.txt").read_text(encoding="utf-8").splitlines():
    if "|" not in linea or linea.startswith("tabla"):
        continue
    tabla, cols = [x.strip() for x in linea.split("|", 1)]
    real[tabla] = set(c.strip() for c in cols.split(","))

# ----------------------------------------------------------- el diff --------
problemas = 0

solo_diseno = sorted(set(diseno) - set(real))
solo_base = sorted(t for t in set(real) - set(diseno) if t not in FASE_0)

if solo_diseno:
    print(f"TABLAS documentadas que NO existen: {', '.join(solo_diseno)}")
    problemas += len(solo_diseno)

if solo_base:
    print(f"TABLAS en la base SIN documentar: {', '.join(solo_base)}")
    problemas += len(solo_base)

for tabla in sorted(set(diseno) & set(real)):
    faltan = sorted(diseno[tabla] - real[tabla])
    sobran = sorted(real[tabla] - diseno[tabla])
    if faltan:
        print(f"  {tabla}: documentada pero NO en la base -> {', '.join(faltan)}")
        problemas += len(faltan)
    if sobran:
        print(f"  {tabla}: en la base pero NO documentada -> {', '.join(sobran)}")
        problemas += len(sobran)

print()
print(f"tablas en el diseno: {len(diseno)}    columnas comparadas: "
      f"{sum(len(c) for c in diseno.values())}")
print(f"desajustes: {problemas}")
sys.exit(1 if problemas else 0)
