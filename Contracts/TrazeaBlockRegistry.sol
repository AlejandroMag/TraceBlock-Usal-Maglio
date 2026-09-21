// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

/// @title  TrazeaBlockRegistry
/// @notice Registro inmutable de donaciones y evidencias de TraceBlock en Polygon.
///         Solo la billetera de la plataforma puede escribir; cualquiera puede leer.
///         No se guarda ningún dato personal: solo identificadores anónimos, montos y hashes.
///
///         Desplegar en Polygon Amoy (testnet, chain id 80002) desde Remix con MetaMask.
///         La cuenta que despliega queda fijada como "plataforma".
contract TrazeaBlockRegistry {
    /// @notice Única dirección autorizada a registrar (la billetera de TraceBlock).
    address public immutable plataforma;

    /// @notice Bloque en el que se registró cada hash por primera vez (0 = nunca registrado).
    ///         Permite verificar un registro con una simple lectura, sin depender de la app.
    mapping(bytes32 => uint256) public bloqueDeRegistro;

    event DonacionRegistrada(
        bytes32 indexed tokenId,
        uint256 montoCentavos,
        uint256 indexed proyectoId,
        uint256 timestamp
    );

    event EvidenciaRegistrada(
        bytes32 indexed hashArchivo,
        uint256 indexed proyectoId,
        uint256 timestamp
    );

    error SoloPlataforma();

    constructor() {
        plataforma = msg.sender;
    }

    modifier soloPlataforma() {
        if (msg.sender != plataforma) revert SoloPlataforma();
        _;
    }

    /// @notice Registra una donación. `tokenId` es el identificador anónimo de TraceBlock
    ///         y `montoCentavos` el monto en centavos de peso (evita decimales en cadena).
    /// @dev    Es idempotente: si el mismo hash se registra dos veces (p. ej. un reintento),
    ///         conserva el bloque original y no revierte, para no dejar reintentos trabados.
    function registrarDonacion(bytes32 tokenId, uint256 montoCentavos, uint256 proyectoId)
        external
        soloPlataforma
    {
        if (bloqueDeRegistro[tokenId] == 0) bloqueDeRegistro[tokenId] = block.number;
        emit DonacionRegistrada(tokenId, montoCentavos, proyectoId, block.timestamp);
    }

    /// @notice Registra la huella SHA-256 de un documento de evidencia de un proyecto.
    function registrarEvidencia(bytes32 hashArchivo, uint256 proyectoId)
        external
        soloPlataforma
    {
        if (bloqueDeRegistro[hashArchivo] == 0) bloqueDeRegistro[hashArchivo] = block.number;
        emit EvidenciaRegistrada(hashArchivo, proyectoId, block.timestamp);
    }

    /// @notice Verificación pública: ¿este hash fue registrado alguna vez?
    function estaRegistrado(bytes32 hash) external view returns (bool) {
        return bloqueDeRegistro[hash] != 0;
    }
}
