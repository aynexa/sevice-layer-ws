const estadoSesion = document.querySelector("#estado-sesion");

function mensajeDeRed(error) {
  if (error instanceof TypeError) {
    return "El navegador no llegó a la API. El proyecto tiene que estar en ejecución y la página debe abrirse en http://localhost:5080.";
  }
  return error.message;
}

function basesApi() {
  const bases = [];
  if (location.protocol === "http:" || location.protocol === "https:")
    bases.push("");
  bases.push("http://localhost:5080");
  return [...new Set(bases)];
}

function mostrarError(id, mensaje) {
  const nodo = document.querySelector(id);
  nodo.hidden = !mensaje;
  nodo.textContent = mensaje || "";
}

function ocupar(boton, ocupado) {
  boton.disabled = ocupado;
}

async function leerJson(respuesta) {
  const cuerpo = await respuesta.json().catch(() => ({}));
  if (!respuesta.ok) {
    throw new Error(cuerpo.error || "No se pudo completar la consulta.");
  }
  return cuerpo;
}

function pintarSesion(sesion) {
  estadoSesion.textContent = `Sesión de ${sesion.usuario}`;
  estadoSesion.classList.add("activa");
  document.querySelector("#sesion-usuario").textContent = sesion.usuario;
  document.querySelector("#sesion-company").textContent = sesion.companyDb;
  document.querySelector("#sesion-url").textContent = sesion.serviceLayerUrl;
  document.querySelector("#sesion-id").textContent = sesion.sessionId;
  document.querySelector("#datos-sesion").hidden = false;
}

function texto(valor) {
  return valor ? valor : "—";
}

function fecha(valor) {
  if (!valor) return "—";
  const fechaValor = new Date(valor);
  if (Number.isNaN(fechaValor.getTime())) return valor;
  return fechaValor.toLocaleDateString("es-PE");
}

function monto(valor) {
  if (valor === null || valor === undefined) return "—";
  return Number(valor).toLocaleString("es-PE", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function estadoDocumento(valor) {
  if (valor === "bost_Open") return "Abierta";
  if (valor === "bost_Close") return "Cerrada";
  return texto(valor);
}

function tipoSocio(valor) {
  if (valor === "cCustomer") return "Cliente";
  if (valor === "cSupplier") return "Proveedor";
  if (valor === "cLid") return "Lead";
  return texto(valor);
}

function pintarOrden(consulta) {
  const orden = consulta.orden;
  const campos = [
    ["DocEntry", orden.docEntry],
    ["DocNum", orden.docNum],
    ["Cliente", orden.cardCode],
    ["Nombre", orden.cardName],
    ["Fecha", fecha(orden.docDate)],
    ["Entrega", fecha(orden.docDueDate)],
    ["Estado", estadoDocumento(orden.docStatus)],
    ["Total", monto(orden.docTotal)],
    ["Pagado", monto(orden.paidToDate)],
    ["Ref. cliente", texto(orden.numAtCard)],
    ["Comentarios", texto(orden.comments)]
  ];

  const lista = document.querySelector("#datos-orden");
  lista.replaceChildren();
  for (const [titulo, valor] of campos) {
    const bloque = document.createElement("div");
    const etiqueta = document.createElement("dt");
    const dato = document.createElement("dd");
    etiqueta.textContent = titulo;
    dato.textContent = valor ?? "—";
    bloque.append(etiqueta, dato);
    lista.append(bloque);
  }
  lista.hidden = false;
  const recurso = document.querySelector("#recurso-orden");
  recurso.hidden = false;
  recurso.textContent = `${consulta.metodo} ${consulta.recurso}`;
}

function pintarSocios(consulta) {
  const cuerpo = document.querySelector("#cuerpo-socios");
  cuerpo.replaceChildren();
  for (const socio of consulta.socios) {
    const fila = document.createElement("tr");
    for (const valor of [socio.cardCode, socio.cardName, tipoSocio(socio.cardType), socio.federalTaxID, socio.emailAddress]) {
      const celda = document.createElement("td");
      celda.textContent = texto(valor);
      fila.append(celda);
    }
    cuerpo.append(fila);
  }

  const hayDatos = consulta.socios.length > 0;
  document.querySelector("#wrap-socios").hidden = !hayDatos;
  document.querySelector("#vacio-socios").hidden = hayDatos;
  const recurso = document.querySelector("#recurso-socios");
  recurso.hidden = false;
  recurso.textContent = `${consulta.metodo} ${consulta.recurso}\n${consulta.encabezado}`;
}

async function consultar(ruta) {
  let ultimoError;
  for (const base of basesApi()) {
    try {
      return await leerJson(await fetch(`${base}${ruta}`));
    } catch (error) {
      ultimoError = error;
      if (!(error instanceof TypeError))
        throw new Error(error.message);
    }
  }
  throw new Error(mensajeDeRed(ultimoError));
}

consultar("/api/salud").catch((error) => {
  const aviso = document.querySelector("#aviso-api");
  aviso.hidden = false;
  aviso.textContent = error.message;
});

document.querySelector("#btn-sesion").addEventListener("click", async (evento) => {
  const boton = evento.currentTarget;
  ocupar(boton, true);
  mostrarError("#error-sesion", "");
  try {
    pintarSesion(await consultar("/api/sesion"));
  } catch (error) {
    mostrarError("#error-sesion", error.message);
  } finally {
    ocupar(boton, false);
  }
});

document.querySelector("#form-orden").addEventListener("submit", async (evento) => {
  evento.preventDefault();
  const boton = evento.currentTarget.querySelector("button");
  const docEntry = document.querySelector("#doc-entry").value;
  ocupar(boton, true);
  mostrarError("#error-orden", "");
  try {
    const consulta = await consultar(`/api/ordenes/${encodeURIComponent(docEntry)}`);
    pintarOrden(consulta);
  } catch (error) {
    document.querySelector("#datos-orden").hidden = true;
    document.querySelector("#recurso-orden").hidden = true;
    mostrarError("#error-orden", mensajeDeRed(error));
  } finally {
    ocupar(boton, false);
  }
});

document.querySelector("#form-socios").addEventListener("submit", async (evento) => {
  evento.preventDefault();
  const boton = evento.currentTarget.querySelector("button");
  const tamano = document.querySelector("#tamano").value;
  ocupar(boton, true);
  mostrarError("#error-socios", "");
  try {
    const consulta = await consultar(`/api/socios?tamano=${encodeURIComponent(tamano)}`);
    pintarSocios(consulta);
  } catch (error) {
    document.querySelector("#wrap-socios").hidden = true;
    document.querySelector("#vacio-socios").hidden = true;
    document.querySelector("#recurso-socios").hidden = true;
    mostrarError("#error-socios", mensajeDeRed(error));
  } finally {
    ocupar(boton, false);
  }
});
