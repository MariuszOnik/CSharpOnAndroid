// Edytor kodu C# dla aplikacji GamePlayer.
// Komunikacja z C#:
//   JS -> C#: Android.loadCode(), Android.save(code), Android.compile(code), Android.complete(id, code, pos)
//   C# -> JS: window.onCompileResult(json), window.onCompletions(id, json)
import { EditorView, basicSetup } from "codemirror";
import { EditorState } from "@codemirror/state";
import { keymap } from "@codemirror/view";
import { indentWithTab } from "@codemirror/commands";
import { StreamLanguage, indentUnit, HighlightStyle, syntaxHighlighting } from "@codemirror/language";
import { csharp } from "@codemirror/legacy-modes/mode/clike";
import { lintGutter, setDiagnostics } from "@codemirror/lint";
import { oneDarkTheme } from "@codemirror/theme-one-dark";
import { tags as t } from "@lezer/highlight";

const bridge = window.Android;
const status = document.getElementById("status");
const playButton = document.getElementById("play");

// Kolory składni bez czerwieni (One Dark ma czerwone zmienne i właściwości).
const highlight = HighlightStyle.define([
  { tag: [t.keyword, t.modifier, t.operatorKeyword, t.controlKeyword], color: "#c678dd" },
  { tag: [t.typeName, t.className, t.namespace], color: "#e5c07b" },
  { tag: [t.function(t.variableName), t.function(t.propertyName)], color: "#61afef" },
  { tag: [t.variableName, t.propertyName, t.name], color: "#d7dae0" },
  { tag: [t.definition(t.variableName)], color: "#9fd3ff" },
  { tag: [t.string, t.special(t.string), t.character], color: "#98c379" },
  { tag: [t.number, t.bool, t.null, t.atom], color: "#d19a66" },
  { tag: [t.operator, t.punctuation, t.bracket], color: "#abb2bf" },
  { tag: [t.comment, t.lineComment, t.blockComment], color: "#7f848e", fontStyle: "italic" },
  { tag: [t.meta, t.annotation], color: "#56b6c2" },
]);

// --- IntelliSense: podpowiedzi liczy Roslyn po stronie C# ---
let nextRequestId = 1;
const pendingCompletions = new Map();

window.onCompletions = (id, result) => {
  const resolve = pendingCompletions.get(id);
  if (!resolve) return;
  pendingCompletions.delete(id);
  resolve(result);
};

function roslynCompletions(context) {
  if (!bridge) return null;
  const word = context.matchBefore(/\w*/);
  const afterDot = context.state.sliceDoc(word.from - 1, word.from) === ".";
  if (!context.explicit && !afterDot && word.from === word.to) return null;

  const id = nextRequestId++;
  return new Promise((resolve) => {
    pendingCompletions.set(id, resolve);
    bridge.complete(id, context.state.doc.toString(), context.pos);
  }).then((result) =>
    result && result.options.length
      ? {
          from: result.from,
          options: result.options.map((o) => ({ label: o.label, type: o.type, detail: o.detail ?? undefined })),
          validFor: /^\w*$/,
        }
      : null
  );
}

// --- Autozapis ---
let saveTimer = 0;
function scheduleSave() {
  clearTimeout(saveTimer);
  saveTimer = setTimeout(saveNow, 1000);
}
function saveNow() {
  clearTimeout(saveTimer);
  if (bridge) setStatus(bridge.save(view.state.doc.toString()), "muted");
}
document.addEventListener("visibilitychange", () => {
  if (document.visibilityState === "hidden") saveNow();
});

const view = new EditorView({
  parent: document.getElementById("editor"),
  state: EditorState.create({
    doc: bridge ? bridge.loadCode() : "// brak mostka Android\n",
    extensions: [
      basicSetup,
      keymap.of([indentWithTab]),
      indentUnit.of("    "),
      StreamLanguage.define(csharp),
      EditorState.languageData.of(() => [{ autocomplete: roslynCompletions }]),
      lintGutter(),
      oneDarkTheme,
      syntaxHighlighting(highlight),
      EditorView.updateListener.of((update) => {
        if (update.docChanged) scheduleSave();
      }),
      EditorView.theme({ "&": { height: "100%" }, ".cm-scroller": { fontSize: "14px" } }),
    ],
  }),
});

function setStatus(text, kind) {
  status.textContent = text;
  status.className = kind || "";
}

playButton.addEventListener("click", () => {
  if (!bridge) return;
  clearTimeout(saveTimer);
  playButton.disabled = true;
  setStatus("Kompiluję…");
  view.dispatch(setDiagnostics(view.state, []));
  // Oddajemy klatkę przeglądarce, żeby status zdążył się odświeżyć.
  setTimeout(() => bridge.compile(view.state.doc.toString()), 16);
});

window.onCompileResult = (result) => {
  playButton.disabled = false;
  const length = view.state.doc.length;
  const diagnostics = result.diagnostics.map((d) => ({
    from: Math.min(d.from, length),
    to: Math.min(Math.max(d.to, d.from), length),
    severity: d.severity,
    message: d.message,
  }));
  view.dispatch(setDiagnostics(view.state, diagnostics));

  const errors = result.diagnostics.filter((d) => d.severity === "error");
  if (result.ok) {
    setStatus(`OK (${result.ms} ms)`, "ok");
  } else if (errors.length) {
    const first = errors[0];
    setStatus(`${errors.length} bł. — linia ${first.line}: ${first.message}`, "error");
    view.dispatch({ selection: { anchor: Math.min(first.from, length) }, scrollIntoView: true });
  } else {
    setStatus(result.message || "Błąd", "error");
  }
};

// Pasek symboli, których brakuje na klawiaturze telefonu.
for (const button of document.querySelectorAll("#symbols button")) {
  button.addEventListener("mousedown", (e) => e.preventDefault()); // nie zabieraj fokusu edytorowi
  button.addEventListener("click", () => {
    const text = button.dataset.insert ?? button.textContent;
    view.dispatch(view.state.replaceSelection(text));
    view.focus();
  });
}
